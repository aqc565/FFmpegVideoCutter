namespace FFmpegVideoCutter.Models;

public record VideoItem
{
    public string FilePath { get; init; } = "";
    public string FileName => Path.GetFileName(FilePath);
    public int Width { get; init; }
    public int Height { get; init; }
    public string VideoCodec { get; init; } = "";
    public double BitRate { get; init; }
    public double Duration { get; init; }
    public string AudioCodec { get; init; } = "";
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public VideoStatus Status { get; set; } = VideoStatus.Pending;
    public string ErrorMessage { get; set; } = "";

    public string ResolutionText => Width > 0 ? $"{Width}×{Height}" : "-";
    public string RangeText => $"{StartTime:hh\\:mm\\:ss}-{EndTime:hh\\:mm\\:ss}";
    public string StatusText => Status switch
    {
        VideoStatus.Pending => "待处理",
        VideoStatus.Processing => "处理中",
        VideoStatus.Completed => "完成",
        VideoStatus.Failed => "失败",
        _ => "",
    };
}
