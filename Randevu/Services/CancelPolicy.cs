using Randevu.Entities;

namespace Randevu.Services;

public interface ICancelPolicy
{
    bool CanCancel(DateTime nowLocal, Appointment appt, BookingWeek activeWeek);
    string WhyNot(DateTime nowLocal, Appointment appt, BookingWeek activeWeek);
}

// Kural:
// - Aktif hafta randevuları: Perşembe 17:00 kapanışına kadar iptal, sonrası iptal yok
// - Diğer haftalar: en geç 2 saat önce iptal
public class CancelPolicy : ICancelPolicy
{
    public bool CanCancel(DateTime nowLocal, Appointment appt, BookingWeek activeWeek)
    {
        bool isInActiveWeek = appt.Date >= activeWeek.Monday && appt.Date <= activeWeek.Sunday;

        if (isInActiveWeek)
            return nowLocal < activeWeek.WindowCloseLocal;

        var apptLocal = new DateTime(appt.Date.Year, appt.Date.Month, appt.Date.Day, appt.StartTime.Hour, appt.StartTime.Minute, 0);
        return nowLocal <= apptLocal.AddHours(-2);
    }

    public string WhyNot(DateTime nowLocal, Appointment appt, BookingWeek activeWeek)
    {
        bool isInActiveWeek = appt.Date >= activeWeek.Monday && appt.Date <= activeWeek.Sunday;

        if (isInActiveWeek)
            return $"Bu haftanın randevuları en geç {activeWeek.WindowCloseLocal:dd.MM.yyyy HH:mm} tarihine kadar iptal edilebilir.";

        var apptLocal = new DateTime(appt.Date.Year, appt.Date.Month, appt.Date.Day, appt.StartTime.Hour, appt.StartTime.Minute, 0);
        return $"Randevu en geç {apptLocal.AddHours(-2):dd.MM.yyyy HH:mm} tarihine kadar iptal edilebilir.";
    }
}
