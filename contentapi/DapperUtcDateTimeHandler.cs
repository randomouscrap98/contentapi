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
        DateTime dt;
        if(!DateTime.TryParseExact(value.ToString(), Constants.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out dt))
            throw new InvalidOperationException("Cannot parse datetime from database!");
        //var odt = DateTime.Parse(value.ToString() ?? throw new InvalidOperationException("Cannot parse datetime from database!"));
        //var dt = odt.ToUniversalTime();
        return dt;
    }

    public override void SetValue(IDbDataParameter parameter, DateTime value)
    {
        parameter.Value = value.ToUniversalTime().ToString(Constants.DateFormat);
    }
}