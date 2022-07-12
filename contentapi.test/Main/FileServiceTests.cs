using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using contentapi.data;
using contentapi.Main;
using contentapi.Search;
using contentapi.Utilities;
using Microsoft.Extensions.Logging;
using Xunit;

namespace contentapi.test;

[Collection("PremadeDatabase")]
public class FileServiceTests : ViewUnitTestBase, IDisposable 
{
    protected const string SimpleImageBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAUAAAAFCAIAAAACDbGyAAAAIklEQVQI12P8//8/AxJgYmBgYGRkRJBo8gzI/P///6PLAwAuoA79WVXllAAAAABJRU5ErkJggg==";
    protected static readonly byte[] SimpleImage = Convert.FromBase64String(SimpleImageBase64);

    protected DbUnitTestSearchFixture fixture;
    protected IMapper mapper;
    protected IDbWriter writer;
    protected IGenericSearch searcher;
    protected FileService service;
    protected FileServiceConfig config;
    protected S3Provider s3provider;
    //protected FakeS3Client s3Mock;



    public FileServiceTests(DbUnitTestSearchFixture fixture)
    {
        this.fixture = fixture;
        this.mapper = fixture.GetService<IMapper>();

        //This client throws exceptions for everything
        //s3Mock = new FakeS3Client();
        s3provider = new S3Provider(); //As is, it will returned NotImplemented for IAmazonS3

        searcher = fixture.GetService<IGenericSearch>();
        writer = fixture.GetService<IDbWriter>();
        config = new FileServiceConfig(){
            HighQualityResize = false
        };
        service = new FileService(fixture.GetService<ILogger<FileService>>(), () => writer, () => searcher, config, s3provider,
            new ImageManipulator_Direct(fixture.GetService<ILogger<ImageManipulator_Direct>>()));

        //Always want a fresh database!
        fixture.ResetDatabase();

        //Every test gets their own directory
        config.MainLocation = Guid.NewGuid().ToString() + "_main";
        config.ThumbnailLocation = Guid.NewGuid().ToString() + "_thumbnails";
        config.TempLocation = Guid.NewGuid().ToString() + "_temp(quant)"; 
    }

    public void Dispose()
    {
        if(Directory.Exists(config.MainLocation))
            Directory.Delete(config.MainLocation, true);
        if(Directory.Exists(config.ThumbnailLocation))
            Directory.Delete(config.ThumbnailLocation, true);
        if(Directory.Exists(config.TempLocation))
            Directory.Delete(config.TempLocation, true);
    }

    [Fact]
    public void IsS3_DefaultNo()
    {
        Assert.False(service.IsS3());
    }

    [Fact]
    public async Task S3Commands_Fail()
    {
        using var memstream = new MemoryStream();
        await Assert.ThrowsAnyAsync<NotImplementedException>(() => service.GetS3DataAsync("whatever"));
        await Assert.ThrowsAnyAsync<NotImplementedException>(() => service.PutS3DataAsync(memstream, "whatever", "mimetypessuck"));
    }

    [Fact]
    public void GetThumbnailPath_Empty()
    {
        Assert.Empty(service.GetThumbnailPath("heck", new GetFileModify()));
    }

    [Fact]
    public void GetThumbnailPath_SizeSet()
    {
        var result = service.GetThumbnailPath("heck", new GetFileModify(){ size = 100 });
        Assert.NotEmpty(result);
        Assert.Contains("heck", result);
    }

    [Fact]
    public void GetThumbnailPath_CropSet()
    {
        var result = service.GetThumbnailPath("heck", new GetFileModify(){ crop = true });
        Assert.NotEmpty(result);
        Assert.Contains("heck", result);
    }

    [Fact]
    public void GetThumbnailPath_FreezeSet()
    {
        var result = service.GetThumbnailPath("heck", new GetFileModify(){ freeze = true });
        Assert.NotEmpty(result);
        Assert.Contains("heck", result);
    }

    [Fact]
    public async Task UploadFile_Simple()
    {
        using var memStream = new MemoryStream(SimpleImage);

        var result = await service.UploadFile(new UploadFileConfigExtra() { }, memStream, NormalUserId);

        AssertDateClose(result.createDate);
        Assert.NotEmpty(result.hash);
        Assert.Equal(NormalUserId, result.createUserId);
        Assert.Equal("image/png", result.literalType); //The mimetype
        Assert.Empty(result.name);
    }

    [Fact]
    public async Task UploadFile_GetFile_Simple()
    {
        using var memStream = new MemoryStream(SimpleImage);

        var result = await service.UploadFile(new UploadFileConfigExtra() { }, memStream, NormalUserId);
        var rawInfo = await service.GetFileAsync(result.hash, new GetFileModify());
        
        Assert.Equal("image/png", rawInfo.Item2);
        Assert.True(SimpleImage.SequenceEqual(rawInfo.Item1));
    }

    [Theory]
    [InlineData(0, true)] //Size 0 or less is "no redo"
    [InlineData(-1, true)] //Size 0 or less is "no redo"
    [InlineData(5, false)]
    [InlineData(10, true)]
    [InlineData(100, true)]
    [InlineData(1000, true)]
    [InlineData(2000, false)]
    public async Task GetFile_SizeCheck(int size, bool allowed)
    {
        using var memStream = new MemoryStream(SimpleImage);

        var result = await service.UploadFile(new UploadFileConfigExtra() { }, memStream, NormalUserId);

        if(allowed)
        {
            var rawInfo = await service.GetFileAsync(result.hash, new GetFileModify());
            Assert.Equal("image/png", rawInfo.Item2);

            if(size <= 0)
                Assert.True(SimpleImage.SequenceEqual(rawInfo.Item1));
        }
        else
        {
            await Assert.ThrowsAnyAsync<RequestException>(() => service.GetFileAsync(result.hash, new GetFileModify() { size = size }));
        }
    }
}