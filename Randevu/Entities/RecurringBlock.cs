namespace Randevu.Entities;

public enum RecurringBlockType
{
    Daily = 1,   // her gün
    Weekly = 2   // haftanın seçili günleri
}

// Tekrarlayan blok:
// - Daily: her gün X saat aralığı
// - Weekly: seçili günler (Mon..Sun) X saat aralığı
public class RecurringBlock
{
    public long Id { get; set; }
    public int SalonId { get; set; }

    public RecurringBlockType Type { get; set; }

    // Weekly için: 0..6 (Sun..Sat) yerine biz net olsun diye 1..7 (Mon=1..Sun=7) kullanacağız
    // Daily için null bırakılır
    public int? DayOfWeekIso { get; set; } // 1=Mon ... 7=Sun

    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    public int? ChairNo { get; set; } // null => tüm koltuklar

    public string Reason { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
