using FFmpegVideoCutter.Services;
using Xunit;

namespace FFmpegVideoCutter.Tests;

public class VideoInfoServiceTests
{
    private const string SampleJson = """
        {
          "streams": [
            {
              "codec_type": "video",
              "width": 1920,
              "height": 1080,
              "r_frame_rate": "24000/1001",
              "codec_name": "h264",
              "bit_rate": "8000000"
            },
            {
              "codec_type": "audio",
              "codec_name": "aac",
              "sample_rate": "48000",
              "channels": 6
            }
          ],
          "format": {
            "duration": "8130.000000",
            "size": "2576980377"
          }
        }
        """;

    [Fact]
    public void Parse_ExtractsFields()
    {
        var info = VideoInfoService.Parse(SampleJson);
        Assert.Equal(1920, info.Width);
        Assert.Equal(1080, info.Height);
        Assert.Equal("h264", info.VideoCodec);
        Assert.Equal("aac", info.AudioCodec);
        Assert.Equal(6, info.Channels);
        Assert.Equal(48000, info.AudioSampleRate);
        Assert.Equal(8130.0, info.Duration);
        Assert.Equal(2576980377L, info.Size);
        Assert.Equal(8000000L, info.BitRate);
        Assert.InRange(info.FrameRate, 23.97, 23.98);
    }

    [Fact]
    public void Parse_MissingStreamBitRate_FallsBackToFormat()
    {
        const string json = """
            {
              "streams": [
                { "codec_type": "video", "width": 640, "height": 480, "codec_name": "h264" }
              ],
              "format": { "bit_rate": "1000000", "duration": "10.0" }
            }
            """;
        var info = VideoInfoService.Parse(json);
        Assert.Equal(1000000L, info.BitRate);
    }

    [Fact]
    public void Parse_AttachedPic_Skipped()
    {
        const string json = """
            {
              "streams": [
                { "codec_type": "video", "width": 300, "height": 300, "codec_name": "mjpeg", "disposition": { "attached_pic": 1 } },
                { "codec_type": "audio", "codec_name": "mp3" }
              ],
              "format": { "duration": "180.0" }
            }
            """;
        var info = VideoInfoService.Parse(json);
        Assert.False(info.HasVideo);
        Assert.Equal("mp3", info.AudioCodec);
    }

    [Fact]
    public void Parse_NoAudio()
    {
        const string json = """
            {
              "streams": [
                { "codec_type": "video", "width": 1280, "height": 720, "codec_name": "hevc" }
              ],
              "format": { "duration": "5.0" }
            }
            """;
        var info = VideoInfoService.Parse(json);
        Assert.True(info.HasVideo);
        Assert.False(info.HasAudio);
    }
}
