using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Randevu.Data;
using Randevu.Entities;
using Randevu.Hubs;
using Randevu.Models.Dto;
using Randevu.Services;
using System.Security.Claims;

namespace Randevu.Controllers;

[Authorize]
[ApiController]
[Route("api/booking")]
public class BookingController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IHubContext<BookingHub> _hub;
    private readonly ITimeService _time;
    private readonly IBookingWindowService _window;
    private readonly ISlotRules _slotRules;
    private readonly IEligibilityService _elig;
    private readonly ICancelPolicy _cancelPolicy;
    private readonly IBlockService _blockService;

    public BookingController(
        AppDbContext db,
        IHubContext<BookingHub> hub,
        ITimeService time,
        IBookingWindowService window,
        ISlotRules slotRules,
        IEligibilityService elig,
        ICancelPolicy cancelPolicy,
        IBlockService blockService)
    {
        _db = db; _hub = hub; _time = time; _window = window;
        _slotRules = slotRules; _elig = elig; _cancelPolicy = cancelPolicy; _blockService = blockService;
    }

    // Bu endpoint manipülasyona kapalı:
    // - tarih parametresi almaz
    // - sadece server'ın hesapladığı "aktif hafta"yı döndürür
    [HttpGet("week/{salonId:int}")]
    public async Task<ActionResult<WeekSnapshotDto>> GetWeek(int salonId, CancellationToken ct)
    {
        var now = _time.NowIstanbul();
        var week = _window.GetCurrentWeek(now);

        var userName = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userName))
            return Unauthorized("Kullanıcı adı bulunamadı.");

        var salon = await _db.Salons.FirstOrDefaultAsync(s => s.Id == salonId && s.IsActive, ct);
        if (salon is null) return NotFound("Salon bulunamadı veya pasif.");

        // Aktif haftaya ait randevular
        var appts = await _db.Appointments
            .Where(a => a.IsActive && a.SalonId == salonId && a.Date >= week.Monday && a.Date <= week.Sunday)
            .ToListAsync(ct);

        // Tekil bloklar (range)
        var manualBlocks = await _blockService.GetManualBlocksForRangeAsync(salonId, week.Monday, week.Sunday, ct);

        // Tekrarlayan bloklar (gün bazlı hesaplanacak)
        var recurringBlocks = await _blockService.GetRecurringBlocksAsync(salonId, ct);

        var dto = new WeekSnapshotDto
        {
            Monday = week.Monday.ToString("yyyy-MM-dd"),
            Sunday = week.Sunday.ToString("yyyy-MM-dd"),
            IsWindowOpen = _window.IsWindowOpen(now),
            WindowCloseLocal = week.WindowCloseLocal.ToString("dd.MM.yyyy HH:mm")
        };

        // Kullanıcı sadece kendi randevularını görür
        dto.MyAppointments = appts.Where(a => a.UserName == userName)
            .Select(a => new MyAppointmentDto
            {
                Id = a.Id,
                SalonId = a.SalonId,
                ChairNo = a.ChairNo,
                Date = a.Date.ToString("yyyy-MM-dd"),
                StartTime = a.StartTime.ToString("HH:mm")
            }).ToList();

        // Slot üretimi: (gün x saat x koltuk)
        foreach (var day in EachDay(week.Monday, week.Sunday))
        {
            int isoDow = IsoDow(day);

            foreach (var t in _slotRules.EnumerateDailySlots())
            {
                for (int chair = 1; chair <= salon.ChairCount; chair++)
                {
                    var slot = new SlotStateDto
                    {
                        ChairNo = chair,
                        Date = day.ToString("yyyy-MM-dd"),
                        StartTime = t.ToString("HH:mm"),
                        Status = "Active"
                    };

                    // 1) Tekil blok
                    bool isManualBlocked = manualBlocks.Any(b =>
                        b.Date == day &&
                        (b.ChairNo == null || b.ChairNo == chair) &&
                        t >= b.StartTime && t < b.EndTime);

                    // 2) Tekrarlayan blok
                    bool isRecurringBlocked = recurringBlocks.Any(r =>
                        (r.ChairNo == null || r.ChairNo == chair) &&
                        t >= r.StartTime && t < r.EndTime &&
                        (
                            r.Type == RecurringBlockType.Daily ||
                            (r.Type == RecurringBlockType.Weekly && r.DayOfWeekIso == isoDow)
                        ));

                    if (isManualBlocked || isRecurringBlocked)
                    {
                        slot.Status = "Closed";
                    }
                    else
                    {
                        var a = appts.FirstOrDefault(x => x.Date == day && x.StartTime == t && x.ChairNo == chair);
                        if (a != null)
                        {
                            slot.Status = "Booked";

                            // Güvenlik: dolu slotta sadece "kendi randevusu" ise isim göster
                            if (a.UserName == userName)
                            {
                                slot.IsMine = true;
                                slot.DisplayName = a.DisplayName;
                                slot.AppointmentId = a.Id;
                            }
                        }
                    }

                    dto.Slots.Add(slot);
                }
            }
        }

        return dto;
    }

    // Randevu alma - rate limit + antiforgery zorunlu
    [EnableRateLimiting("booking-policy")]
    [ValidateAntiForgeryToken]
    [HttpPost("book")]
    public async Task<IActionResult> Book([FromBody] BookRequestDto req, CancellationToken ct)
    {
        var now = _time.NowIstanbul();
        var week = _window.GetCurrentWeek(now);

        // Kural: pencere kapalıysa booking yok
        var desc = User.FindFirst("Description")?.Value ?? "";
        bool isEngineer = desc.Contains("Müh.", StringComparison.OrdinalIgnoreCase);

        if (!_window.CanUserBookNow(now, isEngineer))
            return BadRequest("Şu an randevu alma işlemi kapalı.");

        var userName = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userName))
            return Unauthorized("Kullanıcı adı bulunamadı.");

        // Güvenlik: request parsing
        if (!DateOnly.TryParse(req.Date, out var date))
            return BadRequest("Tarih formatı hatalı (yyyy-MM-dd).");

        if (!TimeOnly.TryParse(req.StartTime, out var startTime))
            return BadRequest("Saat formatı hatalı (HH:mm).");

        // Güvenlik: sadece aktif hafta günleri (Pazartesi-Pazar)
        // Böylece 16.01.2026 ve önceki günlere işlem zaten çıkamaz.
        if (date < week.Monday || date > week.Sunday)
            return BadRequest("Sadece aktif haftanın (Pazartesi-Pazar) günlerinden randevu alabilirsiniz.");

        // Güvenlik: salon aktif mi
        var salon = await _db.Salons.FirstOrDefaultAsync(s => s.Id == req.SalonId && s.IsActive, ct);
        if (salon is null) return BadRequest("Salon bulunamadı veya pasif.");

        // Güvenlik: koltuk numarası salon sınırında mı
        if (req.ChairNo < 1 || req.ChairNo > salon.ChairCount)
            return BadRequest("Geçersiz koltuk.");

        // Güvenlik: saat slot kurallarına uygun mu (mola dahil)
        if (!_slotRules.IsValidSlot(startTime))
            return BadRequest("Seçilen saat randevu saatleri içinde değil.");

        // 14 gün kuralı (server-side kesin kontrol)
        var (ok14, reason14) = await _elig.Check14DayRuleAsync(userName, date, ct);
        if (!ok14) return BadRequest(reason14);

        // Blok kontrol (tekil + tekrarlı)
        bool blocked = await _blockService.IsBlockedAsync(req.SalonId, date, startTime, req.ChairNo, ct);
        if (blocked) return BadRequest("Seçilen slot kapalı (bloklu).");

        // DisplayName: sadece kendi randevusunda görünecek
        var displayName = $"{User.FindFirst(ClaimTypes.GivenName)?.Value} {User.FindFirst(ClaimTypes.Surname)?.Value} ({desc})";

        var appt = new Appointment
        {
            SalonId = req.SalonId,
            ChairNo = req.ChairNo,
            Date = date,
            StartTime = startTime,
            UserName = userName,
            DisplayName = displayName,
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        _db.Appointments.Add(appt);

        try
        {
            // Unique index burada son kilit: aynı slot iki kişi alamaz
            await _db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            return BadRequest("Bu koltuk bu saatte az önce doldu. Lütfen yenileyip tekrar deneyin.");
        }

        // SignalR yayın (aktif haftanın grubu)
        var groupKey = $"salon:{req.SalonId}:week:{week.Monday:yyyy-MM-dd}";
        await _hub.Clients.Group(groupKey).SendAsync("SlotChanged", new
        {
            salonId = req.SalonId,
            date = req.Date,
            startTime = req.StartTime,
            chairNo = req.ChairNo,
            status = "Booked"
        }, ct);

        return Ok(new { id = appt.Id });
    }

    // Randevu iptal - rate limit + antiforgery zorunlu
    [EnableRateLimiting("booking-policy")]
    [ValidateAntiForgeryToken]
    [HttpPost("cancel")]
    public async Task<IActionResult> Cancel([FromBody] CancelRequestDto req, CancellationToken ct)
    {
        var now = _time.NowIstanbul();
        var week = _window.GetCurrentWeek(now);

        // Kural: pencere kapandıysa iptal de yok
        if (!_window.IsWindowOpen(now))
            return BadRequest("Şu an iptal işlemi kapalı.");

        var userName = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(userName))
            return Unauthorized("Kullanıcı adı bulunamadı.");

        var appt = await _db.Appointments.FirstOrDefaultAsync(a => a.Id == req.AppointmentId && a.IsActive, ct);
        if (appt is null) return NotFound("Randevu bulunamadı.");

        // Güvenlik: sadece kendi randevusunu iptal edebilir
        if (appt.UserName != userName)
            return Forbid();

        if (!_cancelPolicy.CanCancel(now, appt, week))
            return BadRequest(_cancelPolicy.WhyNot(now, appt, week));

        appt.IsActive = false;
        await _db.SaveChangesAsync(ct);

        // SignalR: slot yeniden Active
        var groupKey = $"salon:{appt.SalonId}:week:{week.Monday:yyyy-MM-dd}";
        await _hub.Clients.Group(groupKey).SendAsync("SlotChanged", new
        {
            salonId = appt.SalonId,
            date = appt.Date.ToString("yyyy-MM-dd"),
            startTime = appt.StartTime.ToString("HH:mm"),
            chairNo = appt.ChairNo,
            status = "Active"
        }, ct);

        return Ok();
    }

    private static IEnumerable<DateOnly> EachDay(DateOnly start, DateOnly end)
    {
        for (var d = start; d <= end; d = d.AddDays(1))
            yield return d;
    }

    private static int IsoDow(DateOnly d)
    {
        // Monday=1..Sunday=7
        int dow = (int)d.DayOfWeek; // Sun=0
        return dow == 0 ? 7 : dow;
    }
}
