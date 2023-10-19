using System.Data;
using System.Globalization;
using Dapper;

namespace contentapi;

/// <summary>
/// A class required to store datetime values as true ISO UTC
/// </summary>
public class DapperUtcDateTimeHandler : SqlMapper.TypeHandler<DateTime>
{
    public override DateTime Parse(object value)
    {
        DateTime dt = DateTime.Parse(value.ToString() ?? throw new InvalidOperationException("Cannot parse datetime from database!"),
            CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
        return dt;
    }

    public override void SetValue(IDbDataParameter parameter, DateTime value)
    {
        parameter.Value = Constants.ToCommonDateString(value);
    }
}