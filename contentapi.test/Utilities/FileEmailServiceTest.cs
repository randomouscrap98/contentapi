using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using contentapi.Utilities;
using Microsoft.Extensions.Logging;
using Xunit;

namespace contentapi.test;

public class FileEmailServiceTest : UnitTestBase
{
    protected FileEmailService service;
    protected FileEmailServiceConfig config;

    public FileEmailServiceTest()
    {
        config = new FileEmailServiceConfig() { Folder = Path.Combine("fileEmailTest", Guid.NewGuid().ToString()) };
        service = new FileEmailService(GetService<ILogger<FileEmailService>>(), config);
    }

    [Fact]
    public async Task SendEmailAsync_NoErrors()
    {
        //Just send a basic email and make sure it doesn't fail
        await service.SendEmailAsync(new EmailMessage() {
            Recipients = new List<string> { "test@gmail.com" },
            Title = "A simple title",
            Body = "you\ngot\nbodied"
        });
    }

    [Fact]
    public async Task SendEmailAsync_FunkyChars()
    {
        //Just send a basic email and make sure it doesn't fail
        await service.SendEmailAsync(new EmailMessage() {
            Recipients = new List<string> { "test!#$%&'*+-/=?^_`{|}~wow@gmail.com" },
            Title = "/\\'\"**&%$^#@!ButThink About__-TheCHILDREN??+={}[]|;:><,.`~yeah.",
            Body = "you\ngot\nbodied"
        });
    }

    [Fact]
    public async Task SendEmailAsync_ActuallyCheck()
    {
        var email = new EmailMessage() {
            Recipients = new List<string> { "kyle.megaman@gmail.com" },
            Title = "A simple title: The Registrationing",
            Body = "Attention, Thrillseekers!\n\nYeah I don't know, 859834$8()^&!~+_)**{}|\\\";:><.,/??"
        };
        await service.SendEmailAsync(email);
        var folders = Directory.GetDirectories(config.Folder); //.Select(x => x + Path.DirectorySeparatorChar);
        var theFolder = folders.First(x => Path.GetFileName(x)!.StartsWith("kyle.megaman"));
        var files = Directory.GetFiles(theFolder);
        Assert.Single(files);
        var filename = Path.GetFileName(files.First());
        Assert.Contains("A simple title", filename);
        Assert.Contains("The Registrationing", filename);
        Assert.EndsWith(".txt", filename);
        Assert.Equal(email.Body, await File.ReadAllTextAsync(files.First()));
    }
}
