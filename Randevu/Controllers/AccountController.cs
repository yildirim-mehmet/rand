using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

namespace Randevu.Controllers;

public class AccountController : Controller
{
    private readonly IConfiguration _cfg;
    public AccountController(IConfiguration cfg) => _cfg = cfg;

    // Ana kapı site: /Account/Verify?token=...
    public async Task<IActionResult> Verify(string token)
    {
        if (string.IsNullOrEmpty(token))
            return Unauthorized("Erişim reddedildi: Token yok.");

        try
        {
            var secret = _cfg["JwtGate:SecretKey"]!;
            var issuer = _cfg["JwtGate:Issuer"]!;
            var audience = _cfg["JwtGate:Audience"]!;

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(secret);

            tokenHandler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,
                ClockSkew = TimeSpan.Zero
            }, out SecurityToken validatedToken);

            var jwtToken = (JwtSecurityToken)validatedToken;

            // Token claim'lerini cookie'ye taşı
            var claimsIdentity = new System.Security.Claims.ClaimsIdentity(
                jwtToken.Claims,
                CookieAuthenticationDefaults.AuthenticationScheme);

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new System.Security.Claims.ClaimsPrincipal(claimsIdentity));

            return RedirectToAction("Index", "Salons");
        }
        catch
        {
            return BadRequest("Kimlik doğrulama başarısız. Lütfen Ana site üzerinden tekrar deneyin.");
        }
    }

    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("LoginInfo", "Home");
    }
}
