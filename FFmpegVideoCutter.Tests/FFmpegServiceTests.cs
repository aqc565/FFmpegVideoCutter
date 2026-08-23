using FFmpegVideoCutter.Models;
using FFmpegVideoCutter.Services;
using Xunit;

namespace FFmpegVideoCutter.Tests;

public class FFmpegServiceTests
{
    [Fact]
    public void BuildArguments_Copy_ContainsCopyAndOutputSeek()
    {
        var job = new CutJob
        {
            InputPath = @"C:\videos\a b.mp4",
            OutputPath = @"C:\out\out.mp4",
            Start = TimeSpan.FromSeconds(5),
            End = TimeSpan.FromSeconds(10),
            Profile = EncodeProfile.Copy,
        };

        var args = FFmpegService.BuildArguments(job);

        Assert.Contains("-i \"C:\\videos\\a b.mp4\"", args);
        Assert.Contains("-ss 5 ", args);
        Assert.Contains("-to 10 ", args);
        Assert.Contains("-c copy", args);
        Assert.Contains("-progress pipe:1", args);
        Assert.Contains("-map 0:v:0", args);
    }

    [Fact]
    public void BuildArguments_Reencode_ContainsCodecAndAudio()
    {
        var profile = EncodeProfile.All.First(p => p.VideoCodec == "libx265");
        var job = new CutJob
        {
            InputPath = @"C:\v\a.mp4",
            OutputPath = @"C:\o\b.mp4",
            Start = TimeSpan.Zero,
            End = TimeSpan.FromSeconds(5),
            Profile = profile,
        };

        var args = FFmpegService.BuildArguments(job);

        Assert.Contains("-c:v libx265", args);
        Assert.Contains("-crf 23", args);
        Assert.Contains("-c:a aac -b:a 128k", args);
    }
}
