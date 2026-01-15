namespace Randevu.Services;

public interface ITimeService
{
    DateTime NowIstanbul();
}

public class TimeService : ITimeService
{
    private readonly TimeZoneInfo _tz;

    public TimeService()
    {
        // Windows
        _tz = TimeZoneInfo.FindSystemTimeZoneById("Turkey Standard Time");
    }

    public DateTime NowIstanbul()
    {
        return TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _tz);
    }
}
