using contentapi.data;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Advanced;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Processing;

namespace contentapi.Utilities;

/// <summary>
/// An image manipulator that directly calls the ImageSharp library, doing all manipulation
/// on threads rather than processes
/// </summary>
public class ImageManipulator_Direct : IImageManipulator
{
    protected ILogger logger;

    public const double ResizeFactor = 0.8;
    public const double ResizeFactorReduce = 0.2;

    //These may not be constants someday, idk
    public const int JpegHighQuality = 97;
    public const int MinJpegHighQualitySize = 100;

    //Don't really know what to do with this right now
    public bool HighQualityResize = true;

    protected static SemaphoreSlim SingleManipLock = new SemaphoreSlim(1, 1);

    public ImageManipulator_Direct(ILogger<ImageManipulator_Direct> logger)
    {
        this.logger = logger;
    }

    /// <summary>
    /// Given some image data as a stream, transform the image to fit within the given size (of bytes) and save to the given savePath
    /// </summary>
    /// <param name="fileData"></param>
    /// <param name="maxSize"></param>
    /// <returns></returns>
    public async Task<ImageManipulationInfo> FitToSizeAndSave(Stream fileData, string savePath, int maxSize) //, double resizeFactor, double resizeFactorReduce)
    {
        var result = new ImageManipulationInfo {
            RenderCount = 0,
            LoadCount = 1,
            SizeInBytes = fileData.Length
        };

        logger.LogTrace($"FitToSize called with size {maxSize}, image bytes {result.SizeInBytes}");

        await SingleManipLock.WaitAsync();

        try
        {
            using var memStream = new MemoryStream();

            await fileData.CopyToAsync(memStream);
            fileData.Seek(0, SeekOrigin.Begin);

            IImageFormat format = await Image.DetectFormatAsync(fileData);
            fileData.Seek(0, SeekOrigin.Begin);

            //This will throw an exception if it's not an image (most likely)
            using (var image = Image.Load(fileData))
            {
                double sizeFactor = ResizeFactor;
                result.Width = image.Width;
                result.Height = image.Height;
                result.MimeType = format.DefaultMimeType;

                //while (fileConfig.tryResize && imageByteCount > config.MaxSize && sizeFactor > 0)
                while (result.SizeInBytes > maxSize && sizeFactor > 0)
                {
                    double resize = 1 / Math.Sqrt(result.SizeInBytes / (maxSize * sizeFactor));
                    logger.LogWarning($"User image too large ({result.SizeInBytes}), trying ONE resize by {resize}");
                    result.Width = (int)(result.Width * resize);
                    result.Height = (int)(result.Height * resize);
                    image.Mutate(x => x.Resize(result.Width, result.Height, KnownResamplers.Lanczos3));
                    result.RenderCount++;

                    memStream.SetLength(0);
                    image.Save(memStream, format);
                    result.SizeInBytes = memStream.Length;

                    //Keep targeting an EVEN more harsh error margin (even if it's incorrect because
                    //it stacks with previous resizes), also this makes the loop guaranteed to end
                    sizeFactor -= ResizeFactorReduce;
                }

                //If the image is still too big, bad ok bye
                if (result.SizeInBytes > maxSize)
                    throw new RequestException("File too large!");
            }

            logger.LogDebug($"New image size: {result.SizeInBytes}");
            memStream.Seek(0, SeekOrigin.Begin);

            using (var stream = System.IO.File.Create(savePath))
            {
                await memStream.CopyToAsync(stream);
            }
        }
        finally
        {
            SingleManipLock.Release();
        }

        return result; 
    }

    /// <summary>
    /// Given an image as a stream, perform the given modifications to it and save it to the given path
    /// </summary>
    /// <param name="fileData"></param>
    /// <param name="savePath"></param>
    /// <param name="modify"></param>
    /// <returns></returns>
    public async Task<ImageManipulationInfo> MakeThumbnailAndSave(Stream fileData, string savePath, GetFileModify modify) //, bool highQualityResize)
    {
        var result = new ImageManipulationInfo {
            RenderCount = 0,
            LoadCount = 1
        };

        await SingleManipLock.WaitAsync();

        try
        {
            await Task.Run(async () =>
            {
                IImageFormat format = await Image.DetectFormatAsync(fileData);
                result.MimeType = format.DefaultMimeType;
                fileData.Seek(0, SeekOrigin.Begin);

                using var image = Image.Load(fileData);

                //var maxDim = Math.Max(image.Width, image.Height);
                var isGif = format.DefaultMimeType == Constants.GifMime;
                var isJpg = format.DefaultMimeType == Constants.JpegMime;

                //Square ALWAYS happens, it can happen before other things.
                if (modify.crop)
                {
                    var minDim = Math.Min(image.Width, image.Height);
                    image.Mutate(x => x.Crop(new Rectangle((image.Width - minDim) / 2, (image.Height - minDim) / 2, minDim, minDim)));
                    result.RenderCount++;
                }

                //This must come after the crop!
                var isNowLarger = (modify.size > Math.Max(image.Width, image.Height));

                //Saving as png also works, but this preserves the format (even if it's a little heavier compute, it's only a one time thing)
                if (modify.freeze && isGif)
                {
                    while (image.Frames.Count > 1)
                        image.Frames.RemoveFrame(1);
                    result.RenderCount++;
                }

                if (modify.size > 0 && !(isGif && isNowLarger)) //&& (modify.size > image.Width || modify.size > image.Height)))
                {
                    var width = 0;
                    var height = 0;

                    //Preserve aspect ratio when not square
                    if (image.Width > image.Height)
                        width = modify.size;
                    else
                        height = modify.size;

                    if (HighQualityResize)
                        image.Mutate(x => x.Resize(width, height, isNowLarger ? KnownResamplers.Spline : KnownResamplers.Lanczos3));
                    else
                        image.Mutate(x => x.Resize(width, height));

                    result.RenderCount++;
                }

                result.Width = image.Width;
                result.Height = image.Height;

                using (var stream = System.IO.File.OpenWrite(savePath))
                {
                    IImageEncoder? encoder = null;

                    if (HighQualityResize && modify.size <= MinJpegHighQualitySize && isJpg)
                    {
                        encoder = new SixLabors.ImageSharp.Formats.Jpeg.JpegEncoder()
                        {
                            Quality = JpegHighQuality,
                        };
                    }

                    if (encoder != null)
                        image.Save(stream, encoder);
                    else
                        image.Save(stream, format);

                    result.SizeInBytes = stream.Length;
                }
            });
        }
        finally
        {
            SingleManipLock.Release();
        }


        return result;
    }
}