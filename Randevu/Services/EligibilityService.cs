using Microsoft.EntityFrameworkCore;
using Randevu.Data;

namespace Randevu.Services;

public interface IEligibilityService
{
    Task<(bool ok, string? reason)> Check14DayRuleAsync(string userName, DateOnly requestedDate, CancellationToken ct);
}

public class EligibilityService : IEligibilityService
{
    private readonly AppDbContext _db;
    public EligibilityService(AppDbContext db) => _db = db;

    public async Task<(bool ok, string? reason)> Check14DayRuleAsync(string userName, DateOnly requestedDate, CancellationToken ct)
    {
        // Kullanıcının en son (aktif) randevusu
        var lastDate = await _db.Appointments
            .Where(a => a.IsActive && a.UserName == userName)
            .OrderByDescending(a => a.Date)
            .Select(a => (DateOnly?)a.Date)
            .FirstOrDefaultAsync(ct);

        if (lastDate is null) return (true, null);

        int diffDays = requestedDate.DayNumber - lastDate.Value.DayNumber;
        if (diffDays < 14)
            return (false, $"Son randevunuz {lastDate:dd.MM.yyyy}. Yeni randevu için en az 14 gün olmalı.");

        return (true, null);
    }
}
