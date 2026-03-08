namespace CnssProxy.Helpers;

public enum DateGrouping { Day, Month, Year }

public class DateGroupingHelper(DateTime startDate, DateTime endDate)
{
    private readonly DateTime _start = startDate.Date;
    private readonly DateTime _end = endDate.Date;

    public DateGrouping Grouping { get; } = Determine(startDate, endDate);

    private static DateGrouping Determine(DateTime s, DateTime e) =>
        (e - s).Days switch
        {
            <= 31 => DateGrouping.Day,
            <= 365 => DateGrouping.Month,
            _ => DateGrouping.Year,
        };

    public string GetKey(DateTime date) => Grouping switch
    {
        DateGrouping.Day => date.ToString("yyyy-MM-dd"),
        DateGrouping.Month => date.ToString("yyyy-MM"),
        DateGrouping.Year => date.Year.ToString(),
        _ => date.ToString("yyyy-MM-dd"),
    };

    public List<string> GetAllKeys()
    {
        var keys = new List<string>();
        var seen = new HashSet<string>();
        var current = _start;
        while (current <= _end)
        {
            var key = GetKey(current);
            if (seen.Add(key)) keys.Add(key);
            current = Grouping switch
            {
                DateGrouping.Day => current.AddDays(1),
                DateGrouping.Month => current.AddMonths(1),
                _ => current.AddYears(1),
            };
        }
        return keys;
    }

    public Dictionary<string, T> FillGaps<T>(Dictionary<string, T> data, T zero) =>
        GetAllKeys().ToDictionary(k => k, k => data.TryGetValue(k, out var v) ? v : zero);
}
