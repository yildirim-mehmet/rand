namespace Randevu.Entities;

public class Appointment
{
    public long Id { get; set; }

    public int SalonId { get; set; }
    public int ChairNo { get; set; } // 1..ChairCount

    public DateOnly Date { get; set; }
    public TimeOnly StartTime { get; set; }

    // Güvenlik: asıl bağlayıcı alan - User.Identity.Name (SamAccountName)
    public string UserName { get; set; } = null!;

    // UI amaçlı: sadece "kendi randevusunda" görünecek metin
    public string DisplayName { get; set; } = "";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    // Concurrency token (zorunlu değil ama audit için faydalı)
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
