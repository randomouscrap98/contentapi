using System;
using AutoMapper;
using contentapi.Main;
using contentapi.Search;
using contentapi.test.Mock;
using Microsoft.Extensions.Logging;
using Xunit;

namespace contentapi.test;

[Collection("PremadeDatabase")]
public class FileServiceTests : ViewUnitTestBase //, IClassFixture<DbUnitTestSearchFixture>
{
    protected const string SimpleImageBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAUAAAAFCAIAAAACDbGyAAAAIklEQVQI12P8//8/AxJgYmBgYGRkRJBo8gzI/P///6PLAwAuoA79WVXllAAAAABJRU5ErkJggg==";
    protected static readonly byte[] SimpleImage = Convert.FromBase64String(SimpleImageBase64);

    protected DbUnitTestSearchFixture fixture;
    protected IMapper mapper;
    protected IDbWriter writer;
    protected IGenericSearch searcher;
    protected FileService service;
    protected FileServiceConfig config;
    protected FakeS3Client s3Mock;



    public FileServiceTests(DbUnitTestSearchFixture fixture)
    {
        this.fixture = fixture;
        this.mapper = fixture.GetService<IMapper>();

        //This client throws exceptions for everything
        s3Mock = new FakeS3Client();

        searcher = fixture.GetService<IGenericSearch>();
        writer = fixture.GetService<IDbWriter>();
        config = new FileServiceConfig();
        service = new FileService(fixture.GetService<ILogger<FileService>>(), writer, searcher, config, s3Mock);

        //Always want a fresh database!
        fixture.ResetDatabase();
    }

    [Fact]
    public void IsS3_DefaultNo()
    {
        Assert.False(service.IsS3());
    }
}