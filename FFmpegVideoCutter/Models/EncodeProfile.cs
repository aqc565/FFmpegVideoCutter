namespace FFmpegVideoCutter.Models;

public record EncodeProfile
{
    public string Name { get; init; } = "";
    public string Extension { get; init; } = ".mp4";
    public string VideoCodec { get; init; } = "";
    public IReadOnlyList<string> ExtraVideoArgs { get; init; } = Array.Empty<string>();

    public bool IsCopy => VideoCodec == "copy";

    public static EncodeProfile Copy { get; } = new()
    {
        Name = "直接复制（无损，最快）",
        Extension = ".mp4",
        VideoCodec = "copy",
    };

    public static IReadOnlyList<EncodeProfile> All { get; } = new EncodeProfile[]
    {
        Copy,
        new() { Name = "H.265 (HEVC)", Extension = ".mp4", VideoCodec = "libx265", ExtraVideoArgs = new[] { "-preset", "medium", "-crf", "23" } },
        new() { Name = "H.264 (AVC)", Extension = ".mp4", VideoCodec = "libx264", ExtraVideoArgs = new[] { "-preset", "medium", "-crf", "23" } },
        new() { Name = "VP9", Extension = ".webm", VideoCodec = "libvpx-vp9", ExtraVideoArgs = new[] { "-crf", "30", "-b:v", "0", "-row-mt", "1" } },
        new() { Name = "AV1", Extension = ".mp4", VideoCodec = "libsvtav1", ExtraVideoArgs = new[] { "-preset", "8", "-crf", "30" } },
    };
}
