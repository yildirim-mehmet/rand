namespace Randevu.Entities;

// Tekil blok: belirli bir tarihte, belirli saat aralığı
public class ManualBlock
{
    public long Id { get; set; }
    public int SalonId { get; set; }
    public DateOnly Date { get; set; }

    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    // null => tüm koltuklar; dolu => sadece o koltuk
    public int? ChairNo { get; set; }

    public string Reason { get; set; } = "";
    public string CreatedBy { get; set; } = "";
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
