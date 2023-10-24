using System.Globalization;

namespace contentapi;

public static class Constants
{
    public const string DefaultHash = "0";
    public const string DateFormat = @"yyyy-MM-ddTHH\:mm\:ss.fffZ";

    //public const string RelatedContentKey = "related_content";
    public const string ParentsKey = "parent";
    public const string CountField = "specialCount";

    public const int GeneralCacheAge = 13824000; //Six months

    public const string GifMime = "image/gif";
    public const string JpegMime = "image/jpeg";

    /// <summary>
    /// Shortcut function to compute the default DateTime string using Constants.DateFormat. This is so 
    /// standardized that I'm fine putting this as a shortcut in Constants
    /// </summary>
    /// <param name="dt"></param>
    /// <returns></returns>
    public static string ToCommonDateString(DateTime dt)
    {
        return dt.ToUniversalTime().ToString(DateFormat, CultureInfo.InvariantCulture);
    }

    public enum StorageKeys
    {
        restarts
    }
}
