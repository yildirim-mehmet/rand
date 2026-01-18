using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Randevu.Controllers;

public class HomeController : Controller
{
    [Authorize]
    public IActionResult Index()
    {
        ViewBag.AdSoyad = $"{User.FindFirst(ClaimTypes.GivenName)?.Value} {User.FindFirst(ClaimTypes.Surname)?.Value}";
        ViewBag.KullaniciAdi = User.Identity?.Name;
        ViewBag.Aciklama = User.FindFirst("Description")?.Value;

        // TC şimdilik yoksayılacak (okumak serbest)
        ViewBag.Tc = User.FindFirst("TC")?.Value;

        return View();
    }

    public IActionResult LoginInfo()
    {
        return View();
        return Content("Oturum kapatıldı veya geçersiz. Lütfen <a href=\"/Admin/Index\">Yönetim</a> Ana Kapı üzerinden giriş yapın.");
    }
}
