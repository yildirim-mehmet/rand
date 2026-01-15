namespace Randevu.Services;

public interface ISlotRules
{
    bool IsValidSlot(TimeOnly startTime);
    IEnumerable<TimeOnly> EnumerateDailySlots();
}

// Slot kuralları sabit:
// 08:30-12:00 (mola: 12:00-13:30)
// 13:30-17:00
// 30 dk grid
public class SlotRules : ISlotRules
{
    public bool IsValidSlot(TimeOnly t)
    {
        bool inMorning = t >= new TimeOnly(8, 30) && t < new TimeOnly(12, 0);
        bool inAfternoon = t >= new TimeOnly(13, 30) && t < new TimeOnly(17, 0);
        if (!(inMorning || inAfternoon)) return false;

        return t.Minute == 0 || t.Minute == 30;
    }

    public IEnumerable<TimeOnly> EnumerateDailySlots()
    {
        for (var t = new TimeOnly(8, 30); t < new TimeOnly(12, 0); t = t.AddMinutes(30))
            yield return t;

        for (var t = new TimeOnly(13, 30); t < new TimeOnly(17, 0); t = t.AddMinutes(30))
            yield return t;
    }
}
