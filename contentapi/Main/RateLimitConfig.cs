namespace contentapi.Main;

public class RateLimitConfig
{
    public Dictionary<string, string> Rates {get;set;} = new Dictionary<string, string>();
    private Dictionary<string, Tuple<int, TimeSpan>>? _parsedRates = null;
    public Dictionary<string, Tuple<int, TimeSpan>> ParsedRates { get {
        if(_parsedRates == null)
        {
            _parsedRates = new Dictionary<string, Tuple<int, TimeSpan>>();

            foreach(var r in Rates)
            {
                var split = r.Value.Split(",".ToCharArray());
                _parsedRates.Add(r.Key, Tuple.Create(int.Parse(split[0]), TimeSpan.FromSeconds(int.Parse(split[1]))));
            }
        }
        return _parsedRates!;
    }}
}
