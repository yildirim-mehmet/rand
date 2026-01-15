using Microsoft.EntityFrameworkCore;
using Randevu.Entities;

namespace Randevu.Data;

public class AppDbContext : DbContext
{
    public DbSet<Salon> Salons => Set<Salon>();

    public DbSet<Chair> Chairs => Set<Chair>();
    public DbSet<TimeSlot> TimeSlots => Set<TimeSlot>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<ManualBlock> ManualBlocks => Set<ManualBlock>();
    public DbSet<RecurringBlock> RecurringBlocks => Set<RecurringBlock>();
    public DbSet<Staff> Staffs => Set<Staff>();

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Concurrency token
        b.Entity<Appointment>()
            .Property(x => x.RowVersion)
            .IsRowVersion()
            .IsConcurrencyToken();

        // Kritik güvenlik: aynı salon+tarih+saat+koltuk tek randevu
        b.Entity<Appointment>()
            .HasIndex(x => new { x.SalonId, x.Date, x.StartTime, x.ChairNo })
            .IsUnique();

        // RecurringBlock: Weekly ise DayOfWeekIso dolu olmalı (DB constraint yerine app tarafında validate edeceğiz)
        base.OnModelCreating(b);
    }
}
