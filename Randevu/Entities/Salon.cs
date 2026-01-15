namespace Randevu.Entities;

public class Salon
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public bool IsActive { get; set; } = true;

    // salon bazlı koltuk sayısı (1-3 genelde)
    public int ChairCount { get; set; } = 1;

    // slot dakikası (şimdilik 30 sabit, ileride esnetilebilir)
    public int SlotMinutes { get; set; } = 30;
}
