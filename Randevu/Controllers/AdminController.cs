using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Randevu.Data;
using Randevu.Entities;
using Randevu.Models.Dto;

namespace Randevu.Controllers;

[Authorize]
public class AdminController : Controller
{
    private readonly AppDbContext _db;

    public AdminController(AppDbContext db) => _db = db;

    // Yardımcı: giriş yapan kullanıcı Yetki=1 mi? Hangi salonlara yetkili?
    private async Task<(bool ok, Staff staff)> RequireAdminAsync()
    {
        var user = User.Identity?.Name ?? "";
        var staff = await _db.Staffs.FirstOrDefaultAsync(s => s.Aktif && s.Kullanici == user);

        // Yetki=1 şart
        if (staff == null || staff.Yetki != 1)
            return (false, new Staff());

        return (true, staff);
    }

    private bool CanAccessSalon(Staff staff, int salonId)
    {
        // staff.SalonId=0 ise hepsini yönetebilir
        return staff.SalonId == 0 || staff.SalonId == salonId;
    }

    public async Task<IActionResult> Index()
    {
        var (ok, staff) = await RequireAdminAsync();
        if (!ok) return Forbid();

        ViewBag.StaffSalonId = staff.SalonId;
        return View();
    }

    // Salon listesi
    public async Task<IActionResult> Salons()
    {
        var (ok, staff) = await RequireAdminAsync();
        if (!ok) return Forbid();

        var q = _db.Salons.AsQueryable();
        if (staff.SalonId != 0)
            q = q.Where(s => s.Id == staff.SalonId);

        var salons = await q.OrderBy(s => s.Name).ToListAsync();
        return View(salons);
    }

    // Salon ekle/düzenle sayfası
    public async Task<IActionResult> SalonEdit(int? id)
    {
        var (ok, staff) = await RequireAdminAsync();
        if (!ok) return Forbid();

        if (id is null)
        {
            // Yeni salon sadece global admin (salonId=0) oluşturabilsin
            if (staff.SalonId != 0) return Forbid();
            return View(new SalonUpsertDto());
        }

        if (!CanAccessSalon(staff, id.Value)) return Forbid();

        var salon = await _db.Salons.FirstOrDefaultAsync(s => s.Id == id.Value);
        if (salon is null) return NotFound();

        return View(new SalonUpsertDto
        {
            Id = salon.Id,
            Name = salon.Name,
            ChairCount = salon.ChairCount,
            IsActive = salon.IsActive
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SalonSave(SalonUpsertDto dto)
    {
        var (ok, staff) = await RequireAdminAsync();
        if (!ok) return Forbid();

        // Güvenlik: ChairCount limit
        if (dto.ChairCount < 1 || dto.ChairCount > 10)
            return BadRequest("Koltuk sayısı 1-10 aralığında olmalı.");

        if (dto.Id is null)
        {
            if (staff.SalonId != 0) return Forbid();

            _db.Salons.Add(new Salon
            {
                Name = dto.Name.Trim(),
                ChairCount = dto.ChairCount,
                IsActive = dto.IsActive,
                SlotMinutes = 30
            });
        }
        else
        {
            if (!CanAccessSalon(staff, dto.Id.Value)) return Forbid();

            var salon = await _db.Salons.FirstOrDefaultAsync(s => s.Id == dto.Id.Value);
            if (salon is null) return NotFound();

            salon.Name = dto.Name.Trim();
            salon.ChairCount = dto.ChairCount;
            salon.IsActive = dto.IsActive;
        }

        await _db.SaveChangesAsync();
        return RedirectToAction("Salons");
    }

    // Blok yönetimi (tekil + tekrarlayan)
    public async Task<IActionResult> Blocks(int salonId)
    {
        var (ok, staff) = await RequireAdminAsync();
        if (!ok) return Forbid();
        if (!CanAccessSalon(staff, salonId)) return Forbid();

        var salon = await _db.Salons.FirstOrDefaultAsync(s => s.Id == salonId);
        if (salon is null) return NotFound();

        ViewBag.SalonId = salonId;
        ViewBag.SalonName = salon.Name;

        // Listelemek için
        var manual = await _db.ManualBlocks.Where(b => b.IsActive && b.SalonId == salonId)
            .OrderByDescending(b => b.Date).ThenBy(b => b.StartTime).Take(200).ToListAsync();

        var recurring = await _db.RecurringBlocks.Where(r => r.IsActive && r.SalonId == salonId)
            .OrderBy(r => r.Type).ThenBy(r => r.DayOfWeekIso).ThenBy(r => r.StartTime).ToListAsync();

        ViewBag.ManualBlocks = manual;
        ViewBag.RecurringBlocks = recurring;

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateManualBlock(ManualBlockCreateDto dto)
    {
        var (ok, staff) = await RequireAdminAsync();
        if (!ok) return Forbid();
        if (!CanAccessSalon(staff, dto.SalonId)) return Forbid();

        if (!DateOnly.TryParse(dto.Date, out var date)) return BadRequest("Tarih formatı hatalı.");
        if (!TimeOnly.TryParse(dto.StartTime, out var st)) return BadRequest("StartTime hatalı.");
        if (!TimeOnly.TryParse(dto.EndTime, out var et)) return BadRequest("EndTime hatalı.");
        if (et <= st) return BadRequest("EndTime > StartTime olmalı.");

        _db.ManualBlocks.Add(new ManualBlock
        {
            SalonId = dto.SalonId,
            Date = date,
            StartTime = st,
            EndTime = et,
            ChairNo = dto.ChairNo,
            Reason = dto.Reason ?? "",
            CreatedBy = User.Identity?.Name ?? ""
        });

        await _db.SaveChangesAsync();
        return RedirectToAction("Blocks", new { salonId = dto.SalonId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateRecurringBlock(RecurringBlockCreateDto dto)
    {
        var (ok, staff) = await RequireAdminAsync();
        if (!ok) return Forbid();
        if (!CanAccessSalon(staff, dto.SalonId)) return Forbid();

        if (!TimeOnly.TryParse(dto.StartTime, out var st)) return BadRequest("StartTime hatalı.");
        if (!TimeOnly.TryParse(dto.EndTime, out var et)) return BadRequest("EndTime hatalı.");
        if (et <= st) return BadRequest("EndTime > StartTime olmalı.");

        var type = (RecurringBlockType)dto.Type;
        if (type == RecurringBlockType.Weekly)
        {
            if (dto.DayOfWeekIso is null || dto.DayOfWeekIso < 1 || dto.DayOfWeekIso > 7)
                return BadRequest("Weekly için DayOfWeekIso 1..7 olmalı.");
        }

        if (type == RecurringBlockType.Daily)
        {
            dto.DayOfWeekIso = null;
        }

        _db.RecurringBlocks.Add(new RecurringBlock
        {
            SalonId = dto.SalonId,
            Type = type,
            DayOfWeekIso = dto.DayOfWeekIso,
            StartTime = st,
            EndTime = et,
            ChairNo = dto.ChairNo,
            Reason = dto.Reason ?? "",
            CreatedBy = User.Identity?.Name ?? ""
        });

        await _db.SaveChangesAsync();
        return RedirectToAction("Blocks", new { salonId = dto.SalonId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DisableManualBlock(long id, int salonId)
    {
        var (ok, staff) = await RequireAdminAsync();
        if (!ok) return Forbid();
        if (!CanAccessSalon(staff, salonId)) return Forbid();

        var b = await _db.ManualBlocks.FirstOrDefaultAsync(x => x.Id == id && x.SalonId == salonId);
        if (b is null) return NotFound();

        b.IsActive = false;
        await _db.SaveChangesAsync();

        return RedirectToAction("Blocks", new { salonId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DisableRecurringBlock(long id, int salonId)
    {
        var (ok, staff) = await RequireAdminAsync();
        if (!ok) return Forbid();
        if (!CanAccessSalon(staff, salonId)) return Forbid();

        var r = await _db.RecurringBlocks.FirstOrDefaultAsync(x => x.Id == id && x.SalonId == salonId);
        if (r is null) return NotFound();

        r.IsActive = false;
        await _db.SaveChangesAsync();

        return RedirectToAction("Blocks", new { salonId });
    }

    // Rapor (salon yetkisine göre)
    public async Task<IActionResult> Report(int salonId)
    {
        var (ok, staff) = await RequireAdminAsync();
        if (!ok) return Forbid();
        if (!CanAccessSalon(staff, salonId)) return Forbid();

        var salon = await _db.Salons.FirstOrDefaultAsync(s => s.Id == salonId);
        if (salon is null) return NotFound();

        ViewBag.SalonId = salonId;
        ViewBag.SalonName = salon.Name;

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReportRun(ReportFilterDto dto)
    {
        var (ok, staff) = await RequireAdminAsync();
        if (!ok) return Forbid();
        if (!CanAccessSalon(staff, dto.SalonId)) return Forbid();

        if (!DateOnly.TryParse(dto.From, out var from)) return BadRequest("From hatalı.");
        if (!DateOnly.TryParse(dto.To, out var to)) return BadRequest("To hatalı.");
        if (to < from) return BadRequest("To >= From olmalı.");

        var rows = await _db.Appointments
            .Where(a => a.IsActive && a.SalonId == dto.SalonId && a.Date >= from && a.Date <= to)
            .OrderBy(a => a.Date).ThenBy(a => a.StartTime).ThenBy(a => a.ChairNo)
            .Select(a => new
            {
                a.Id,
                a.Date,
                a.StartTime,
                a.ChairNo,
                a.UserName,
                a.DisplayName,
                a.CreatedAtUtc
            })
            .ToListAsync();

        // View'a taşı
        ViewBag.SalonId = dto.SalonId;
        ViewBag.From = from.ToString("yyyy-MM-dd");
        ViewBag.To = to.ToString("yyyy-MM-dd");
        ViewBag.Rows = rows;

        return View("Report");
    }
}
