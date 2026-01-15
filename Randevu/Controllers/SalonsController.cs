using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Randevu.Data;
using Randevu.Services;
using System.Security.Claims;

namespace Randevu.Controllers;

[Authorize]
public class SalonsController : Controller
{
    private readonly AppDbContext _db;
    private readonly ITimeService _time;
    private readonly IBookingWindowService _window;

    public SalonsController(AppDbContext db, ITimeService time, IBookingWindowService window)
    {
        _db = db; _time = time; _window = window;
    }

    public async Task<IActionResult> Index()
    {
        var salons = await _db.Salons.Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync();

        var now = _time.NowIstanbul();
        var week = _window.GetCurrentWeek(now);

        ViewBag.WeekMonday = week.Monday.ToString("yyyy-MM-dd");
        ViewBag.WeekSunday = week.Sunday.ToString("yyyy-MM-dd");
        ViewBag.WindowOpen = week.WindowOpenLocal.ToString("dd.MM.yyyy HH:mm");
        ViewBag.WindowClose = week.WindowCloseLocal.ToString("dd.MM.yyyy HH:mm");

        ViewBag.AdSoyad = $"{User.FindFirst(ClaimTypes.GivenName)?.Value} {User.FindFirst(ClaimTypes.Surname)?.Value}";
        ViewBag.KullaniciAdi = User.Identity?.Name;
        ViewBag.Aciklama = User.FindFirst("Description")?.Value;

        return View(salons);
    }

    public async Task<IActionResult> Chairs(int id)
    {
        var salon = await _db.Salons.FirstOrDefaultAsync(s => s.Id == id && s.IsActive);
        if (salon is null) return NotFound();

        var now = _time.NowIstanbul();
        var week = _window.GetCurrentWeek(now);

        ViewBag.SalonId = salon.Id;
        ViewBag.SalonName = salon.Name;
        ViewBag.ChairCount = salon.ChairCount;
        ViewBag.WeekMonday = week.Monday.ToString("yyyy-MM-dd");
        ViewBag.WindowClose = week.WindowCloseLocal.ToString("dd.MM.yyyy HH:mm");

        // Antiforgery token: JS fetch'lerde kullanacağız
        // Razor form helper ile token üretip JS'e meta olarak basacağız.
        return View(salon);
    }
}
