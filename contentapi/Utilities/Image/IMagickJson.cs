namespace contentapi.Utilities;

/// <summary>
/// Simple data object representing the output of imagick's "json" formatted image data.
/// Doesn't include even NEARLY all the fields, only the ones we care about
/// </summary>
public class IMagickJson
{
    public class IMagickImageJson
    {
        public string? name {get;set;}
        public string? format {get;set;}
        public string? mimeType {get;set;}
        public IMagickGeometry? geometry {get;set;}
    }

    public class IMagickGeometry
    {
        public int width {get;set;}
        public int height {get;set;}
    }

    public IMagickImageJson? image {get;set;}
}
