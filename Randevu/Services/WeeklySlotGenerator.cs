using Microsoft.EntityFrameworkCore;
using Randevu.Data;
using Randevu.Entities;

namespace Randevu.Services
{
    public class WeeklySlotGenerator : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public WeeklySlotGenerator(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await TryGenerateAsync();
                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
            }
        }

        private async Task TryGenerateAsync()
        {
            var now = DateTime.Now;

            // Sadece Cuma günü 08:00 sonrası çalışır
            if (now.DayOfWeek != DayOfWeek.Friday)
                return;

            if (now.TimeOfDay < new TimeSpan(8, 0, 0))
                return;

            // Bir sonraki haftanın Pazartesi günü
            var nextMonday = DateOnly.FromDateTime(now)
                .AddDays(((int)DayOfWeek.Monday - (int)now.DayOfWeek + 7) % 7);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Aynı haftanın slotları zaten üretildiyse tekrar üretme
            bool alreadyExists = await db.TimeSlots
                .AnyAsync(x => x.SlotDate == nextMonday);

            if (alreadyExists)
                return;

            await GenerateWeekAsync(db, nextMonday);
        }

        private async Task GenerateWeekAsync(AppDbContext db, DateOnly monday)
        {
            var salons = await db.Salons
                .Where(s => s.IsActive)
                .ToListAsync();

            foreach (var salon in salons)
            {
                for (int day = 0; day < 7; day++)
                {
                    var date = monday.AddDays(day);

                    // Sabah 08:30 - 12:00
                    GenerateSlots(db, salon, date,
                        new TimeSpan(8, 30, 0),
                        new TimeSpan(12, 0, 0));

                    // Öğleden sonra 13:30 - 17:00
                    GenerateSlots(db, salon, date,
                        new TimeSpan(13, 30, 0),
                        new TimeSpan(17, 0, 0));
                }
            }

            await db.SaveChangesAsync();
        }

        private void GenerateSlots(
            AppDbContext db,
            Salon salon,
            DateOnly date,
            TimeSpan start,
            TimeSpan end)
        {
            for (var time = start; time < end; time += TimeSpan.FromMinutes(salon.SlotMinutes))
            {
                // Salon içindeki koltuklar: 1..ChairCount
                for (int koltukNo = 1; koltukNo <= salon.ChairCount; koltukNo++)
                {
                    bool exists = db.TimeSlots.Any(x =>
                        x.SalonId == salon.Id &&
                        x.KoltukId == koltukNo &&
                        x.SlotDate == date &&
                        x.StartTime == time);

                    if (exists)
                        continue;

                    db.TimeSlots.Add(new TimeSlot
                    {
                        SalonId = salon.Id,
                        KoltukId = koltukNo,
                        SlotDate = date,
                        StartTime = time,
                        EndTime = time.Add(TimeSpan.FromMinutes(salon.SlotMinutes)),
                        Status = SlotStatus.Bos
                    });
                }
            }
        }
    }
}
