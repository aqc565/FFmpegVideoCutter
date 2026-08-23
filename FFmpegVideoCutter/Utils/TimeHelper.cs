using System.Globalization;

namespace FFmpegVideoCutter.Utils;

public static class TimeHelper
{
    private static readonly string[] Formats =
    {
        @"hh\:mm\:ss",
        @"hh\:mm\:ss\.f",
        @"hh\:mm\:ss\.ff",
        @"hh\:mm\:ss\.fff",
        @"h\:mm\:ss",
        @"mm\:ss",
    };

    public static string Format(TimeSpan t) => t.ToString(@"hh\:mm\:ss");

    public static bool TryParse(string? s, out TimeSpan result)
    {
        result = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(s)) return false;
        return TimeSpan.TryParseExact(s.Trim(), Formats, CultureInfo.InvariantCulture, out result);
    }

    public static string ToFfmpegTime(TimeSpan t) =>
        t.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture);

    public static bool IsValidRange(TimeSpan start, TimeSpan end, double duration)
    {
        if (start < TimeSpan.Zero) return false;
        if (end <= start) return false;
        if (end.TotalSeconds > duration + 0.001) return false;
        return true;
    }
}
