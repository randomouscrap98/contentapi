public static class Constants
{
    public const string ShortIsoFormatString = "yyyy-MM-ddTHH:mm:ssZ";
    public static string? ShortIsoFormat(DateTime? date) => date?.ToUniversalTime().ToString(ShortIsoFormatString);
}