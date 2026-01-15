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

            if (now.DayOfWeek != DayOfWeek.Friday) return;
            if (now.TimeOfDay < new TimeSpan(8, 0, 0)) return;

            var nextMonday = DateOnly.FromDateTime(now)
                .AddDays(((int)DayOfWeek.Monday - (int)now.DayOfWeek + 7) % 7);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // Aynı haftayı tekrar üretme
            if (await db.TimeSlots.AnyAsync(x => x.SlotDate == nextMonday))
                return;

            await GenerateWeekAsync(db, nextMonday);
        }

        private async Task GenerateWeekAsync(AppDbContext db, DateOnly monday)
        {
            var salons = await db.Salons
                .Where(s => s.Aktif)
                .Include(s => s.Chairs.Where(c => c.Aktif))
                .ToListAsync();

            foreach (var salon in salons)
            {
                foreach (var chair in salon.Chairs)
                {
                    for (int d = 0; d < 7; d++)
                    {
                        var date = monday.AddDays(d);

                        GenerateSlots(db, salon.Id, chair.Id, date,
                            new TimeSpan(8, 30, 0), new TimeSpan(12, 0, 0));

                        GenerateSlots(db, salon.Id, chair.Id, date,
                            new TimeSpan(13, 30, 0), new TimeSpan(17, 0, 0));
                    }
                }
            }

            await db.SaveChangesAsync();
        }

        private void GenerateSlots(
            AppDbContext db,
            int salonId,
            int chairId,
            DateOnly date,
            TimeSpan start,
            TimeSpan end)
        {
            for (var t = start; t < end; t += TimeSpan.FromMinutes(30))
            {
                db.TimeSlots.Add(new TimeSlot
                {
                    SalonId = salonId,
                    KoltukId = chairId,
                    SlotDate = date,
                    StartTime = t,
                    EndTime = t.Add(TimeSpan.FromMinutes(30)),
                    Status = SlotStatus.Bos
                });
            }
        }
    }
}
