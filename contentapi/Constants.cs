namespace contentapi;

public static class Constants
{
    public const string DefaultHash = "0";
    public const string DateFormat = @"yyyy-MM-ddTHH\:mm\:ss.fffZ";

    //public const string RelatedContentKey = "related_content";
    public const string ParentsKey = "parent";


    public static class SelfRun
    {
        public const string RunPrefix = "run=";
        public const string ArgsPrefix = "args=";
        public const string RunImagePrefix = "image.";
        public const string RunImageResize = RunImagePrefix + "size";
        public const string RunImageThumbnail = RunImagePrefix + "thumbnail";
    }
}