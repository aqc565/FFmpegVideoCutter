using FFmpegVideoCutter.Models;

namespace FFmpegVideoCutter.Services;

public sealed class CutService
{
    private readonly FFmpegService _ffmpeg;
    private readonly VideoInfoService _info;

    public CutService(string ffmpegPath, string ffprobePath)
    {
        _ffmpeg = new FFmpegService(ffmpegPath);
        _info = new VideoInfoService(ffprobePath);
    }

    public VideoInfo GetInfo(string path) => _info.GetInfo(path);

    public Task<CutResult> CutAsync(CutJob job, IProgress<CutProgress>? progress, CancellationToken ct) =>
        _ffmpeg.CutAsync(job, progress, ct);

    public HashSet<string> GetAvailableVideoEncoders() => _ffmpeg.GetAvailableVideoEncoders();

    public bool IsEncoderAvailable(string codec, HashSet<string> available) =>
        codec == "copy" || available.Contains(codec);
}
