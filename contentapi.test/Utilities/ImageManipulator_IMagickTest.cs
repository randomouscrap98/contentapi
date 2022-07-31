using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using contentapi.Utilities;
using Microsoft.Extensions.Logging;
using Xunit;

namespace contentapi.test;

public class ImageManipulator_IMagickTest: UnitTestBase
{
    protected ImageManipulator_IMagick service;
    protected ImageManipulator_IMagickConfig config;

    public ImageManipulator_IMagickTest()
    {
        config = new ImageManipulator_IMagickConfig() { TempPath = Path.Combine("imagickTest", Guid.NewGuid().ToString()) };
        service = new ImageManipulator_IMagick(GetService<ILogger<ImageManipulator_IMagick>>(), config);
    }

    public const string RealParseData = @"
[
{
  ""image"": {
    ""name"": ""fyynk[0]"",
    ""format"": ""PNG"",
    ""formatDescription"": ""Portable Network Graphics"",
    ""mimeType"": ""image/png"",
    ""class"": ""PseudoClass"",
    ""geometry"": {
      ""width"": 100,
      ""height"": 83,
      ""x"": 0,
      ""y"": 0
    },
    ""resolution"": {
      ""x"": 37.8,
      ""y"": 37.8
    },
    ""printSize"": {
      ""x"": 2.6455,
      ""y"": 2.6455
    },
    ""units"": ""PixelsPerCentimeter"",
    ""type"": ""Palette"",
    ""endianess"": ""Undefined"",
    ""colorspace"": ""sRGB"",
    ""depth"": 8,
    ""baseDepth"": 8,
    ""channelDepth"": {
      ""red"": 8,
      ""green"": 8,
      ""blue"": 8
    },
    ""pixels"": 10000,
    ""imageStatistics"": {
      ""all"": {
        ""min"": 0,
        ""max"": 255,
        ""mean"": 107.426,
        ""standardDeviation"": 15470.7,
        ""kurtosis"": -0.880219,
        ""skewness"": 0.365331,
        ""entropy"": 0.858119
      }
    },
    ""channelStatistics"": {
      ""red"": {
        ""min"": 36,
        ""max"": 255,
        ""mean"": 157.906,
        ""standardDeviation"": 16067.5,
        ""kurtosis"": -1.27483,
        ""skewness"": 0.176413,
        ""entropy"": 0.85083
      },
      ""green"": {
        ""min"": 0,
        ""max"": 224,
        ""mean"": 96.6295,
        ""standardDeviation"": 16436.3,
        ""kurtosis"": -1.22817,
        ""skewness"": 0.350079,
        ""entropy"": 0.870404
      },
      ""blue"": {
        ""min"": 0,
        ""max"": 178,
        ""mean"": 67.7422,
        ""standardDeviation"": 13908.2,
        ""kurtosis"": -1.1596,
        ""skewness"": 0.527573,
        ""entropy"": 0.853122
      }
    },
    ""colormapEntries"": 206,
    ""colormap"": [
        ""#240000"",""#A77157"",""#753925"",""#DDA177"",""#9D5936"",
      ""#5A1D0C"",""#C5885F"",""#7E4936"",""#EFB88F"",""#662815"",
      ""#490F04"",""#A9633E"",""#AF7A5E"",""#FFC99F"",""#94502F"",
      ""#6D311D"",""#77402F"",""#8E5A45"",""#521506"",""#CF9066"",
      ""#5E200E"",""#410600"",""#B78165"",""#EBB086"",""#F7C197"",
      ""#AD6A45"",""#86513D"",""#A5613D"",""#8D4A2B"",""#D6986E"",
      ""#7C3A1E"",""#A46343"",""#6B2B14"",""#854225"",""#61220F"",
      ""#673123"",""#B6734D"",""#F7C098"",""#571807"",""#F9C199"",
      ""#BC7A53"",""#E6A87F"",""#3C0400"",""#4D1409"",""#84442B"",
      ""#AD6C4A"",""#813C1F"",""#914D2D"",""#B9754E"",""#A15B37"",
      ""#6B2D1A"",""#C48057"",""#9A5534"",""#FFD0A5"",""#632511"",
      ""#BE896A"",""#79361D"",""#C17D56"",""#541D12"",""#7C3B23"",
      ""#F6BE96"",""#440B03"",""#C79172"",""#B47553"",""#702F17"",
      ""#9D6145"",""#A2664A"",""#440F08"",""#4C1206"",""#723017"",
      ""#F7BF98"",""#8B4C33"",""#C48663"",""#F8BF96"",""#682711"",
      ""#8A4729"",""#622719"",""#471007"",""#5A1F11"",""#92543A"",
      ""#5D2213"",""#601F0B"",""#7A3E2A"",""#D59D7A"",""#C9865D"",
      ""#4D180F"",""#74331A"",""#BB7D5A"",""#B16C46"",""#5F291E"",
      ""#AA6E52"",""#EAA97D"",""#894526"",""#D18E65"",""#E3A276"",
      ""#96593E"",""#8E5036"",""#340000"",""#551A0B"",""#733622"",
      ""#C4835C"",""#3E0903"",""#F2B285"",""#CA885F"",""#D49166"",
      ""#DC996F"",""#955232"",""#FFCCA2"",""#5C2519"",""#521609"",
      ""#824631"",""#6E3A2C"",""#A25D3A"",""#CC8B63"",""#5B2921"",
      ""#875641"",""#B26E49"",""#AA6641"",""#4B1710"",""#96624B"",
      ""#FFD9AD"",""#D3936B"",""#CB8E6B"",""#BA7750"",""#A46C53"",
      ""#702F18"",""#9D5B3A"",""#754233"",""#D8966C"",""#914F31"",
      ""#9A5E44"",""#E7B08B"",""#7F4A38"",""#9E6A52"",""#E9AD84"",
      ""#E6AE89"",""#642A1A"",""#8C533C"",""#B47E62"",""#DB9C73"",
      ""#D29672"",""#E1A781"",""#E29F73"",""#E4AA84"",""#C17F58"",
      ""#EFB790"",""#DDA481"",""#572117"",""#663428"",""#945F49"",
      ""#9B644B"",""#ECB38B"",""#B27759"",""#E3A57C"",""#DDA27C"",
      ""#884E38"",""#813E22"",""#A57058"",""#99573A"",""#C48B6B"",
      ""#743C2B"",""#6C3423"",""#6A270F"",""#BC8668"",""#CC9473"",
      ""#B77856"",""#86482F"",""#F1B68D"",""#945B44"",""#A4694D"",
      ""#7F4027"",""#CD9778"",""#B47A5C"",""#2C0000"",""#BE805F"",
      ""#C98760"",""#E9AF88"",""#FFD4A9"",""#C48F70"",""#7C4432"",
      ""#B97F62"",""#CF9977"",""#7D422C"",""#BC8364"",""#844934"",
      ""#D59973"",""#D99F79"",""#4F2119"",""#CD916C"",""#844E3A"",
      ""#F0B790"",""#AB7154"",""#AC755A"",""#632E22"",""#FFDEB2"",
      ""#FAC096"",""#44100A"",""#C68963"",""#F3B88F"",""#8C5642"",
      ""#4E1A12"",""#6F3E31"",""#562219"",""#693629"",""#F8BF98"",
      ""#FFE0B2""
    ],
    ""renderingIntent"": ""Perceptual"",
    ""gamma"": 0.454545,
    ""chromaticity"": {
      ""redPrimary"": {
        ""x"": 0.64,
        ""y"": 0.33
      },
      ""greenPrimary"": {
        ""x"": 0.3,
        ""y"": 0.6
      },
      ""bluePrimary"": {
        ""x"": 0.15,
        ""y"": 0.06
      },
      ""whitePrimary"": {
        ""x"": 0.3127,
        ""y"": 0.329
      }
    },
    ""backgroundColor"": ""#FFFFFF"",
    ""borderColor"": ""#DFDFDF"",
    ""matteColor"": ""#BDBDBD"",
    ""transparentColor"": ""#000000"",
    ""interlace"": ""None"",
    ""intensity"": ""Undefined"",
    ""compose"": ""Over"",
    ""pageGeometry"": {
      ""width"": 100,
      ""height"": 100,
      ""x"": 0,
      ""y"": 0
    },
    ""dispose"": ""Undefined"",
    ""iterations"": 0,
    ""compression"": ""Zip"",
    ""orientation"": ""Undefined"",
    ""properties"": {
      ""date:create"": ""2022-07-30T21:24:16+00:00"",
      ""date:modify"": ""2022-07-30T21:24:16+00:00"",
      ""png:IHDR.bit-depth-orig"": ""8"",
      ""png:IHDR.bit_depth"": ""8"",
      ""png:IHDR.color-type-orig"": ""3"",
      ""png:IHDR.color_type"": ""3 (Indexed)"",
      ""png:IHDR.interlace_method"": ""0 (Not interlaced)"",
      ""png:IHDR.width,height"": ""100, 100"",
      ""png:pHYs"": ""x_res=3780, y_res=3780, units=1"",
      ""png:PLTE.number_colors"": ""206"",
      ""png:sRGB"": ""intent=0 (Perceptual Intent)"",
      ""signature"": ""7592431b1593c74b7fd795d9c90891c3a0623aa1faec65217284984d9747762f""
    },
    ""artifacts"": {
      ""filename"": ""fyynk[0]""
    },
    ""tainted"": false,
    ""filesize"": ""0B"",
    ""numberPixels"": ""10000"",
    ""pixelsPerSecond"": ""10000000000000000B"",
    ""userTime"": ""0.000u"",
    ""elapsedTime"": ""0:01.000"",
    ""version"": ""ImageMagick 6.9.10-23 Q16 x86_64 20190101 https://imagemagick.org""
  }
}
]";

    public const string RealParseDataLegacy = @"
{
  ""image"": {
    ""name"": ""tri_output.gif[0]"",
    ""format"": ""GIF"",
    ""formatDescription"": ""CompuServe graphics interchange format"",
    ""mimeType"": ""image/gif"",
    ""class"": ""PseudoClass"",
    ""geometry"": {
      ""width"": 128,
      ""height"": 128,
      ""x"": 0,
      ""y"": 0
    },
    ""units"": ""Undefined"",
    ""type"": ""Palette"",
    ""endianess"": ""Undefined"",
    ""colorspace"": ""sRGB"",
    ""depth"": 8,
    ""baseDepth"": 8,
    ""channelDepth"": {
      ""red"": 8,
      ""green"": 8,
      ""blue"": 8
    },
    ""pixels"": 16384,
    ""imageStatistics"": {
      ""all"": {
        ""min"": ""1"",
        ""max"": ""240"",
        ""mean"": ""11.5158"",
        ""standardDeviation"": ""35.0279"",
        ""kurtosis"": ""14.9025"",
        ""skewness"": ""3.83255""
      }
    },
    ""channelStatistics"": {
      ""red"": {
        ""min"": ""1"",
        ""max"": ""240"",
        ""mean"": ""11.4542"",
        ""standardDeviation"": ""34.9142"",
        ""kurtosis"": ""14.9506"",
        ""skewness"": ""3.83873""
      },
      ""green"": {
        ""min"": ""1"",
        ""max"": ""235"",
        ""mean"": ""11.5912"",
        ""standardDeviation"": ""35.0795"",
        ""kurtosis"": ""14.7092"",
        ""skewness"": ""3.80947""
      },
      ""blue"": {
        ""min"": ""1"",
        ""max"": ""239"",
        ""mean"": ""11.5022"",
        ""standardDeviation"": ""35.0896"",
        ""kurtosis"": ""15.047"",
        ""skewness"": ""3.84937""
      }
    },
    ""colormapEntries"": 256,
    ""colormap"": [
        ""#010101"",""#090908"",""#0D090E"",""#2E0808"",""#092D08"",
      ""#08082E"",""#241428"",""#560D0E"",""#64160E"",""#0E570D"",
      ""#126611"",""#595A0B"",""#787909"",""#796A18"",""#6A7918"",
      ""#747312"",""#6C6D04"",""#6A5A38"",""#745434"",""#795131"",
      ""#537334"",""#537434"",""#756423"",""#647423"",""#656434"",
      ""#696A28"",""#58620A"",""#0E0C56"",""#111265"",""#5A095B"",
      ""#743454"",""#743554"",""#791A6A"",""#790B79"",""#6A1A79"",
      ""#731274"",""#533574"",""#543475"",""#69296A"",""#752464"",
      ""#653565"",""#642475"",""#5F075B"",""#0B5B5A"",""#386A58"",
      ""#347353"",""#317951"",""#345474"",""#335474"",""#18796A"",
      ""#186B79"",""#097979"",""#127374"",""#067071"",""#286A69"",
      ""#336465"",""#237464"",""#236475"",""#0D615C"",""#535553"",
      ""#545454"",""#754444"",""#655544"",""#654454"",""#694A49"",
      ""#486A49"",""#546444"",""#447444"",""#446454"",""#494A69"",
      ""#544465"",""#445565"",""#444475"",""#606040"",""#BA3909"",
      ""#BB2A18"",""#AB3A18"",""#B53413"",""#AF2E04"",""#BA1A28"",
      ""#BA0A38"",""#AB1A38"",""#B61433"",""#AD062C"",""#953434"",
      ""#953434"",""#AB2929"",""#A63423"",""#AE242B"",""#9B0E0D"",
      ""#DB1908"",""#DB0A18"",""#CB1A18"",""#D51312"",""#EB0B09"",
      ""#E70D0B"",""#F00C0B"",""#CB2A09"",""#CE2D04"",""#C62A15"",
      ""#CD082A"",""#C71323"",""#D0221C"",""#995909"",""#92510C"",
      ""#9A4B18"",""#8A5A18"",""#945412"",""#AB4A09"",""#AE4C04"",
      ""#A64414"",""#8A6A09"",""#8D6C05"",""#866610"",""#A56502"",
      ""#954423"",""#855423"",""#854433"",""#8A4A28"",""#C54602"",
      ""#9A1A49"",""#9A0B58"",""#8A1A58"",""#951453"",""#8E054C"",
      ""#AC094A"",""#A71444"",""#8B284A"",""#8A2B48"",""#8B096B"",
      ""#861465"",""#A40466"",""#C50445"",""#38BA08"",""#32B414"",
      ""#31B217"",""#349434"",""#339633"",""#08BA38"",""#14B432"",
      ""#17B330"",""#33A423"",""#23B523"",""#23A433"",""#28AA28"",
      ""#0F9C0D"",""#589A09"",""#57970B"",""#588B18"",""#539315"",
      ""#499918"",""#6A8A09"",""#6D8809"",""#648F09"",""#4AAB07"",
      ""#44A414"",""#4BAF04"",""#66A402"",""#548523"",""#449523"",
      ""#448534"",""#498A28"",""#18D908"",""#16CC17"",""#08DA18"",
      ""#09D917"",""#12D311"",""#29CB07"",""#23C414"",""#29CF04"",
      ""#09EB09"",""#0DEA0B"",""#08CA29"",""#0BC927"",""#18DA17"",
      ""#099A58"",""#099A57"",""#159453"",""#189252"",""#348544"",
      ""#239544"",""#238554"",""#288A49"",""#08AA4A"",""#14A444"",
      ""#04AF4A"",""#098A6A"",""#088B6B"",""#098C68"",""#02A466"",
      ""#29C320"",""#848502"",""#343495"",""#343594"",""#381AAB"",
      ""#380ABA"",""#281ABA"",""#3314B6"",""#2E07AF"",""#183AAB"",
      ""#1829BB"",""#0939BA"",""#0437B4"",""#1334B5"",""#042AB2"",
      ""#2929AB"",""#2334A6"",""#2B24AE"",""#100D9B"",""#581A8A"",
      ""#580B9A"",""#541495"",""#491899"",""#6B098B"",""#641485"",
      ""#49298A"",""#443485"",""#442495"",""#542485"",""#4A09AC"",
      ""#4413A7"",""#6604A4"",""#185A8A"",""#184B9A"",""#085999"",
      ""#125494"",""#334485"",""#235485"",""#234495"",""#284A8A"",
      ""#096A8A"",""#066B8C"",""#106687"",""#094AAB"",""#044DAE"",
      ""#1045A8"",""#0265A4"",""#181ACB"",""#180ADB"",""#0819DB"",
      ""#1212D5"",""#2A08CD"",""#2413C7"",""#092ACB"",""#072BCD"",
      ""#0A2DC8"",""#090BEB"",""#0B0DE7"",""#0C0CEF"",""#211FCC"",
      ""#0246C5"",""#4504C4"",""#850486"",""#028585"",""#000000"",
      ""#000000""
    ],
    ""renderingIntent"": ""Perceptual"",
    ""gamma"": 0.454545,
    ""chromaticity"": {
      ""redPrimary"": {
        ""x"": 0.64,
        ""y"": 0.33
      },
      ""greenPrimary"": {
        ""x"": 0.3,
        ""y"": 0.6
      },
      ""bluePrimary"": {
        ""x"": 0.15,
        ""y"": 0.06
      },
      ""whitePrimary"": {
        ""x"": 0.3127,
        ""y"": 0.329
      }
    },
    ""backgroundColor"": ""#000000"",
    ""borderColor"": ""#DFDFDF"",
    ""matteColor"": ""#BDBDBD"",
    ""transparentColor"": ""#000000"",
    ""interlace"": ""None"",
    ""intensity"": ""Undefined"",
    ""compose"": ""Over"",
    ""pageGeometry"": {
      ""width"": 128,
      ""height"": 128,
      ""x"": 0,
      ""y"": 0
    },
    ""dispose"": ""None"",
    ""delay"": ""2x100""
    ""iterations"": 0,
    ""compression"": ""LZW"",
    ""orientation"": ""Undefined"",
    ""properties"": {
      ""date:create"": ""2022-07-31T20:02:58+00:00"",
      ""date:modify"": ""2022-07-31T20:02:58+00:00"",
      ""signature"": ""bf7afbea7cc9dbc84c4ce1b445b00b3ca3647e59682404f5181051ae8458f673""
    },
    ""artifacts"": {
      ""filename"": ""tri_output.gif[0]""
    },
    ""tainted"": false,
    ""filesize"": ""147KB"",
    ""numberPixels"": ""16.4K"",
    ""pixelsPerSecond"": ""16.384EB"",
    ""userTime"": ""0.000u"",
    ""elapsedTime"": ""0:01.000"",
    ""version"": ""ImageMagick 6.9.7-4 Q16 x86_64 20170114 http://www.imagemagick.org""
  }
}
";

    [Fact]
    public void ParseImageManipulationInfo_RealData()
    {
        var parsed = service.ParseImageManipulationInfo(RealParseData);
        Assert.Equal(100, parsed.Width);
        Assert.Equal(83, parsed.Height);
        Assert.Equal("image/png", parsed.MimeType);
    }

    [Fact]
    public void ParseImageManipulationInfo_RealDataLegacy()
    {
        var parsed = service.ParseImageManipulationInfo(RealParseDataLegacy);
        Assert.Equal(128, parsed.Width);
        Assert.Equal(128, parsed.Height);
        Assert.Equal("image/gif", parsed.MimeType);
    }
}