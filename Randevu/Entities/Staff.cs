namespace Randevu.Entities;

public class Staff
{
    public int Id { get; set; }
    public string Kullanici { get; set; } = null!; // SamAccountName
    public string Sifre { get; set; } = "";        // şimdilik kullanılmayabilir
    public int SalonId { get; set; }               // 0 => tüm salonlar
    public int Yetki { get; set; }                 // 1 => yönetim
    public bool Aktif { get; set; } = true;
    public string? Tc { get; set; }                // şimdilik boş
}
