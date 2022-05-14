public static class StaticUtils
{
    public static string HumanTime(TimeSpan time, int decimals = 1)
    {
        double number = 0;
        string units = "???";

        if(time.TotalDays > 365)
        {
            number = time.TotalDays / 365;
            units = "year";
        }
        else if(time.TotalDays >= 1)
        {
            number = time.TotalDays;
            units = "day";
        }
        else if(time.TotalHours >= 1)
        {
            number = time.TotalHours;
            units = "hour";
        }
        else if(time.TotalMinutes >= 1)
        {
            number = time.TotalMinutes;
            units = "minute";
        }
        else if(time.TotalSeconds >= 3)
        {
            number = time.TotalSeconds;
            units = "second";
        }
        else
        {
            number = time.TotalMilliseconds;
            units = "millisecond";
        }

        var numberString = number.ToString($"F{decimals}");

        return $"{numberString} {units}{(number == 1 ? "" : "s")}";
    }
}