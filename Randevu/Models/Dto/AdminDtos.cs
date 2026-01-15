namespace Randevu.Models.Dto;

public class SalonUpsertDto
{
    public int? Id { get; set; }
    public string Name { get; set; } = "";
    public int ChairCount { get; set; } = 1;
    public bool IsActive { get; set; } = true;
}

public class ManualBlockCreateDto
{
    public int SalonId { get; set; }
    public string Date { get; set; } = "";      // yyyy-MM-dd
    public string StartTime { get; set; } = ""; // HH:mm
    public string EndTime { get; set; } = "";   // HH:mm
    public int? ChairNo { get; set; }           // null => tüm koltuklar
    public string Reason { get; set; } = "";
}

public class RecurringBlockCreateDto
{
    public int SalonId { get; set; }
    public int Type { get; set; }               // 1=Daily, 2=Weekly
    public int? DayOfWeekIso { get; set; }      // Weekly için 1..7
    public string StartTime { get; set; } = ""; // HH:mm
    public string EndTime { get; set; } = "";   // HH:mm
    public int? ChairNo { get; set; }
    public string Reason { get; set; } = "";
}

public class ReportFilterDto
{
    public int SalonId { get; set; }
    public string From { get; set; } = ""; // yyyy-MM-dd
    public string To { get; set; } = "";   // yyyy-MM-dd
}
