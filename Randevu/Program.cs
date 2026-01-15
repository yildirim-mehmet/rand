using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Randevu.Data;
using Randevu.Hubs;
using Randevu.Services;
using System;
using System.Threading.RateLimiting;
using Microsoft.EntityFrameworkCore;


var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();

//slolarý ðretmek içn
builder.Services.AddHostedService<WeeklySlotGenerator>();


// EF Core - MSSQL
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Cookie Auth (Verify token -> cookie)
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "RandevuApp_Session";
        options.LoginPath = "/Home/LoginInfo";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
    });

builder.Services.AddAuthorization();

// SignalR
builder.Services.AddSignalR();

// Antiforgery: JS fetch'lerde header ile token göndereceðiz
builder.Services.AddAntiforgery(opt =>
{
    // JS tarafý bu header adýný kullanacak
    opt.HeaderName = "X-CSRF-TOKEN";
});

// Rate limiting (özellikle book/cancel için)
builder.Services.AddRateLimiter(options =>
{
    // book/cancel için kullanýcý bazlý limit:
    // - ayný kullanýcý saniyede max 2 istek
    // - burst: 4
    options.AddPolicy("booking-policy", httpContext =>
    {
        var user = httpContext.User?.Identity?.Name ?? "anon";
        return RateLimitPartition.GetTokenBucketLimiter(user, _ => new TokenBucketRateLimiterOptions
        {
            TokenLimit = 4,
            TokensPerPeriod = 2,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            QueueLimit = 0,
            AutoReplenishment = true
        });
    });
});

// Services
builder.Services.AddSingleton<ITimeService, TimeService>();                 // Istanbul time
builder.Services.AddScoped<IBookingWindowService, BookingWindowService>();  // Cuma->Perþembe pencere
builder.Services.AddScoped<ISlotRules, SlotRules>();                        // 08:30-12:00 / 13:30-17:00
builder.Services.AddScoped<IEligibilityService, EligibilityService>();      // 14 gün kuralý
builder.Services.AddScoped<ICancelPolicy, CancelPolicy>();                 // iptal kuralý
builder.Services.AddScoped<IBlockService, BlockService>();                 // tekil + tekrarlý blok

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

// Rate limit middleware
app.UseRateLimiter();

app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// SignalR
app.MapHub<BookingHub>("/bookingHub");

// MVC routing
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Salons}/{action=Index}/{id?}");

app.Run();


//using Microsoft.AspNetCore.Authentication.Cookies;

//var builder = WebApplication.CreateBuilder(args);

//// MVC Servisleri
//builder.Services.AddControllersWithViews();

//// 1. Kimlik Doðrulama Servisi (Cookie Tabanlý)
//builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
//    .AddCookie(options =>
//    {
//        options.Cookie.Name = "RandevuApp_Session";
//        options.LoginPath = "/Home/LoginInfo"; // Giriþ yapmamýþ kiþiyi buraya atar
//    });

//var app = builder.Build();

//if (!app.Environment.IsDevelopment())
//{
//    app.UseExceptionHandler("/Home/Error");
//}

//app.UseStaticFiles();
//app.UseRouting();

//// 2. Auth Middleware sýralamasý çok önemli!
//app.UseAuthentication();
//app.UseAuthorization();

//app.MapControllerRoute(
//    name: "default",
//    pattern: "{controller=Home}/{action=Index}/{id?}");

//app.Run();


//var builder = WebApplication.CreateBuilder(args);

//// Add services to the container.
//builder.Services.AddControllersWithViews();

//var app = builder.Build();

//// Configure the HTTP request pipeline.
//if (!app.Environment.IsDevelopment())
//{
//    app.UseExceptionHandler("/Home/Error");
//}
//app.UseStaticFiles();

//app.UseRouting();

//app.UseAuthorization();

//app.MapControllerRoute(
//    name: "default",
//    pattern: "{controller=Home}/{action=Index}/{id?}");

//app.Run();
