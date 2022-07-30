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

    [Fact]
    public void ParseImageManipulationInfo_RealData()
    {
        var parsed = service.ParseImageManipulationInfo(RealParseData);
        Assert.Equal(100, parsed.Width);
        Assert.Equal(83, parsed.Height);
        Assert.Equal("image/png", parsed.MimeType);
    }
}