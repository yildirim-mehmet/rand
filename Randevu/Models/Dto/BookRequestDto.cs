namespace Randevu.Models.Dto;

public class BookRequestDto
{
    public int SalonId { get; set; }
    public int ChairNo { get; set; }
    public string Date { get; set; } = "";      // yyyy-MM-dd
    public string StartTime { get; set; } = ""; // HH:mm
}
