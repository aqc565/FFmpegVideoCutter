namespace FFmpegVideoCutter.Models;

public record CutJob
{
    public string InputPath { get; init; } = "";
    public string OutputPath { get; init; } = "";
    public TimeSpan Start { get; init; }
    public TimeSpan End { get; init; }
    public EncodeProfile Profile { get; init; } = EncodeProfile.Copy;
    public bool ForceReencode { get; init; }
}

public record CutProgress
{
    public TimeSpan Position { get; init; }
    public double Percent { get; init; }
}

public record CutResult
{
    public bool Success { get; init; }
    public int ExitCode { get; init; }
    public string Error { get; init; } = "";
}

public record CutError
{
    public string FileName { get; init; } = "";
    public int ErrorCode { get; init; }
    public DateTime Time { get; init; }
    public string Message { get; init; } = "";
}
