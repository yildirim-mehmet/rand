using Microsoft.EntityFrameworkCore;
using Randevu.Data;
using Randevu.Entities;

namespace Randevu.Services;

public interface IBlockService
{
    Task<bool> IsBlockedAsync(int salonId, DateOnly date, TimeOnly startTime, int chairNo, CancellationToken ct);
    Task<List<ManualBlock>> GetManualBlocksForRangeAsync(int salonId, DateOnly from, DateOnly to, CancellationToken ct);
    Task<List<RecurringBlock>> GetRecurringBlocksAsync(int salonId, CancellationToken ct);
}

public class BlockService : IBlockService
{
    private readonly AppDbContext _db;
    public BlockService(AppDbContext db) => _db = db;

    public async Task<bool> IsBlockedAsync(int salonId, DateOnly date, TimeOnly startTime, int chairNo, CancellationToken ct)
    {
        // 1) Tekil blok
        bool manual = await _db.ManualBlocks.AnyAsync(b =>
            b.IsActive &&
            b.SalonId == salonId &&
            b.Date == date &&
            (b.ChairNo == null || b.ChairNo == chairNo) &&
            startTime >= b.StartTime && startTime < b.EndTime, ct);

        if (manual) return true;

        // 2) Tekrarlayan blok
        int isoDow = ToIsoDayOfWeek(date); // 1..7

        bool recurring = await _db.RecurringBlocks.AnyAsync(r =>
            r.IsActive &&
            r.SalonId == salonId &&
            (r.ChairNo == null || r.ChairNo == chairNo) &&
            startTime >= r.StartTime && startTime < r.EndTime &&
            (
                r.Type == RecurringBlockType.Daily ||
                (r.Type == RecurringBlockType.Weekly && r.DayOfWeekIso == isoDow)
            ), ct);

        return recurring;
    }

    public Task<List<ManualBlock>> GetManualBlocksForRangeAsync(int salonId, DateOnly from, DateOnly to, CancellationToken ct)
    {
        return _db.ManualBlocks
            .Where(b => b.IsActive && b.SalonId == salonId && b.Date >= from && b.Date <= to)
            .ToListAsync(ct);
    }

    public Task<List<RecurringBlock>> GetRecurringBlocksAsync(int salonId, CancellationToken ct)
    {
        return _db.RecurringBlocks
            .Where(r => r.IsActive && r.SalonId == salonId)
            .ToListAsync(ct);
    }

    private static int ToIsoDayOfWeek(DateOnly date)
    {
        // .NET: Monday=1 ... Sunday=0
        var dow = (int)date.DayOfWeek; // Sunday=0, Monday=1...
        return dow == 0 ? 7 : dow;     // Sunday->7
    }
}
