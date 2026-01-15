namespace Randevu.Services;

public record BookingWeek(
    DateOnly Monday,
    DateOnly Sunday,
    DateOnly OpenFriday,
    DateTime WindowOpenLocal,
    DateTime WindowCloseLocal
);

public interface IBookingWindowService
{
    BookingWeek GetCurrentWeek(DateTime nowLocal);
    bool IsWindowOpen(DateTime nowLocal);
    bool CanUserBookNow(DateTime nowLocal, bool isEngineer);
}

// Bu servis tamamen "server clock" ile çalışır.
// Kullanıcı farklı date/time göndererek kural dışına çıkamaz.
public class BookingWindowService : IBookingWindowService
{
    private static readonly TimeOnly EngineerOpen = new(8, 0);
    private static readonly TimeOnly GeneralOpen = new(8, 30);
    private static readonly TimeOnly CloseTime = new(17, 0); // Perşembe 17:00

    public BookingWeek GetCurrentWeek(DateTime nowLocal)
    {
        var openFriday = EffectiveOpenFriday(nowLocal);

        // Açılış Cuma -> hedef hafta: Pazartesi (+3) - Pazar (+6)
        var monday = openFriday.AddDays(3);
        var sunday = monday.AddDays(6);

        var windowOpen = new DateTime(openFriday.Year, openFriday.Month, openFriday.Day, 8, 0, 0);

        // kapanış: hedef haftanın Perşembesi 17:00
        var thursday = monday.AddDays(3);
        var windowClose = new DateTime(thursday.Year, thursday.Month, thursday.Day, CloseTime.Hour, CloseTime.Minute, 0);

        return new BookingWeek(monday, sunday, openFriday, windowOpen, windowClose);
    }

    // Cuma 08:00'dan önce "yeni hafta daha açılmadı" => bir önceki Cuma geçerli
    private static DateOnly EffectiveOpenFriday(DateTime nowLocal)
    {
        var today = DateOnly.FromDateTime(nowLocal);
        int dow = (int)nowLocal.DayOfWeek; // Sun=0 ... Fri=5

        int daysSinceFriday = (dow - (int)DayOfWeek.Friday + 7) % 7;
        var lastFriday = today.AddDays(-daysSinceFriday);

        if (today == lastFriday && TimeOnly.FromDateTime(nowLocal) < EngineerOpen)
            lastFriday = lastFriday.AddDays(-7);

        return lastFriday;
    }

    public bool IsWindowOpen(DateTime nowLocal)
    {
        var w = GetCurrentWeek(nowLocal);
        return nowLocal >= w.WindowOpenLocal && nowLocal < w.WindowCloseLocal;
    }

    public bool CanUserBookNow(DateTime nowLocal, bool isEngineer)
    {
        var w = GetCurrentWeek(nowLocal);
        if (nowLocal < w.WindowOpenLocal || nowLocal >= w.WindowCloseLocal) return false;

        // açılış günü 08:00-08:30 sadece Müh.
        if (DateOnly.FromDateTime(nowLocal) == w.OpenFriday)
        {
            var t = TimeOnly.FromDateTime(nowLocal);
            if (t >= EngineerOpen && t < GeneralOpen)
                return isEngineer;
        }

        return true;
    }
}
