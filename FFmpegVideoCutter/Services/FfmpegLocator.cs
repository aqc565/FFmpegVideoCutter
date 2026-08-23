namespace FFmpegVideoCutter.Services;

public sealed class FfmpegLocator
{
    public string? FfmpegPath { get; private set; }
    public string? FfprobePath { get; private set; }

    public bool Locate()
    {
        FfmpegPath = Find("ffmpeg.exe");
        FfprobePath = Find("ffprobe.exe");
        return FfmpegPath is not null && FfprobePath is not null;
    }

    private static string? Find(string exe)
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, exe),
            Path.Combine(@"C:\ffmpeg\bin", exe),
        };

        foreach (var c in candidates)
        {
            if (File.Exists(c)) return c;
        }

        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathVar.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = dir.Trim();
            if (trimmed.Length == 0) continue;
            var p = Path.Combine(trimmed, exe);
            if (File.Exists(p)) return p;
        }

        return null;
    }
}
