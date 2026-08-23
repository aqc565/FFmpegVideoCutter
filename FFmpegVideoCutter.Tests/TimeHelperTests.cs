using FFmpegVideoCutter.Utils;
using Xunit;

namespace FFmpegVideoCutter.Tests;

public class TimeHelperTests
{
    [Theory]
    [InlineData("00:05:30")]
    [InlineData("01:30:00")]
    [InlineData("00:00:30.500")]
    [InlineData("00:00:00")]
    public void TryParse_Valid_ReturnsExpected(string input)
    {
        var ok = TimeHelper.TryParse(input, out var result);
        Assert.True(ok);
        Assert.Equal(TimeSpan.Parse(input, System.Globalization.CultureInfo.InvariantCulture), result);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("99:99:99")]
    public void TryParse_Invalid_ReturnsFalse(string? input)
    {
        Assert.False(TimeHelper.TryParse(input, out _));
    }

    [Fact]
    public void Format_RoundTrips()
    {
        var t = new TimeSpan(1, 2, 3);
        var s = TimeHelper.Format(t);
        Assert.Equal("01:02:03", s);
        Assert.True(TimeHelper.TryParse(s, out var parsed));
        Assert.Equal(t, parsed);
    }

    [Theory]
    [InlineData(0, 10, 100, true)]
    [InlineData(10, 0, 100, false)]
    [InlineData(0, 101, 100, false)]
    [InlineData(-1, 10, 100, false)]
    public void IsValidRange_Checks(double s, double e, double d, bool expected)
    {
        Assert.Equal(expected, TimeHelper.IsValidRange(
            TimeSpan.FromSeconds(s), TimeSpan.FromSeconds(e), d));
    }

    [Fact]
    public void ToFfmpegTime_InvariantCulture()
    {
        Assert.Equal("30.5", TimeHelper.ToFfmpegTime(TimeSpan.FromSeconds(30.5)));
    }
}
