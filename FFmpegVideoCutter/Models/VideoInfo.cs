namespace FFmpegVideoCutter.Models;

public record VideoInfo
{
    public int Width { get; init; }
    public int Height { get; init; }
    public double FrameRate { get; init; }
    public string VideoCodec { get; init; } = "";
    public string AudioCodec { get; init; } = "";
    public long BitRate { get; init; }
    public double Duration { get; init; }
    public long Size { get; init; }
    public int AudioSampleRate { get; init; }
    public int Channels { get; init; }

    public bool HasVideo => Width > 0 && Height > 0;
    public bool HasAudio => !string.IsNullOrEmpty(AudioCodec);

    public string Resolution => HasVideo ? $"{Width}×{Height}" : "-";
    public string FrameRateText => FrameRate > 0 ? $"{FrameRate:0.###}fps" : "-";
    public string BitRateText => BitRate > 0 ? $"{BitRate / 1000:0} kbps" : "-";
    public string SizeText => Size > 0 ? FormatBytes(Size) : "-";
    public string DurationText => Duration > 0 ? TimeSpan.FromSeconds(Duration).ToString(@"hh\:mm\:ss") : "-";

    private static string FormatBytes(long bytes)
    {
        const double gb = 1024.0 * 1024 * 1024;
        const double mb = 1024.0 * 1024;
        return bytes >= gb ? $"{bytes / gb:0.00} GB"
             : bytes >= mb ? $"{bytes / mb:0.0} MB"
             : $"{bytes / 1024.0:0} KB";
    }
}
