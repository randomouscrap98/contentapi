using System.Data;
using Dapper;

namespace contentapi;

/// <summary>
/// A class required to store datetime values as true ISO UTC
/// </summary>
public class DapperUtcDateTimeHandler : SqlMapper.TypeHandler<DateTime>
{
    public override DateTime Parse(object value)
    {
        return DateTime.Parse(value.ToString() ?? throw new InvalidOperationException("Cannot parse datetime from database!"));
    }

    public override void SetValue(IDbDataParameter parameter, DateTime value)
    {
        parameter.Value = value.ToUniversalTime().ToString(@"yyyy-MM-ddTHH\:mm\:ss.fffZ");
    }
}