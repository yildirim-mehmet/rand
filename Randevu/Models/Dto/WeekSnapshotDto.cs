namespace Randevu.Models.Dto;

public class WeekSnapshotDto
{
    public string Monday { get; set; } = "";
    public string Sunday { get; set; } = "";
    public bool IsWindowOpen { get; set; }
    public string WindowCloseLocal { get; set; } = "";

    public List<MyAppointmentDto> MyAppointments { get; set; } = new();
    public List<SlotStateDto> Slots { get; set; } = new();
}

public class MyAppointmentDto
{
    public long Id { get; set; }
    public int SalonId { get; set; }
    public int ChairNo { get; set; }
    public string Date { get; set; } = "";
    public string StartTime { get; set; } = "";
}

public class SlotStateDto
{
    public int ChairNo { get; set; }
    public string Date { get; set; } = "";
    public string StartTime { get; set; } = "";

    // Active | Booked | Closed
    public string Status { get; set; } = "Active";

    // sadece kendi randevusunda hover
    public bool IsMine { get; set; }
    public string? DisplayName { get; set; }
    public long? AppointmentId { get; set; }
}
