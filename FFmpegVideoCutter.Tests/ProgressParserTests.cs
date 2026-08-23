using FFmpegVideoCutter.Services;
using Xunit;

namespace FFmpegVideoCutter.Tests;

public class ProgressParserTests
{
    [Fact]
    public void FeedLine_OutTime_Parses()
    {
        var p = new ProgressParser();
        p.FeedLine("out_time=00:01:23.456789");
        Assert.Equal(83.456789, p.Position.TotalSeconds, 6);
        Assert.False(p.Finished);
    }

    [Fact]
    public void FeedLine_End_FlagsFinished()
    {
        var p = new ProgressParser();
        p.FeedLine("progress=continue");
        Assert.False(p.Finished);
        p.FeedLine("progress=end");
        Assert.True(p.Finished);
    }

    [Fact]
    public void FeedLine_IgnoresMalformed()
    {
        var p = new ProgressParser();
        p.FeedLine("no_equals_here");
        p.FeedLine("frame=100");
        Assert.Equal(TimeSpan.Zero, p.Position);
    }
}
