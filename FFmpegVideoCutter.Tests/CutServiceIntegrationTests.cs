using System.Collections.Concurrent;
using System.Diagnostics;
using FFmpegVideoCutter.Models;
using FFmpegVideoCutter.Services;
using Xunit;

namespace FFmpegVideoCutter.Tests;

public class CutServiceIntegrationTests : IDisposable
{
    private static readonly string? Ffmpeg = FindTool("ffmpeg.exe");
    private static readonly string? Ffprobe = FindTool("ffprobe.exe");

    private readonly string _dir;
    private readonly string _source;

    public CutServiceIntegrationTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "ffcut_tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _source = Path.Combine(_dir, "source.mp4");
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    private static string? FindTool(string exe)
    {
        var pathVar = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathVar.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var p = Path.Combine(dir.Trim(), exe);
            if (File.Exists(p)) return p;
        }
        return null;
    }

    private static void RunTool(string exe, string args)
    {
        using var p = Process.Start(new ProcessStartInfo(exe, args)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        })!;
        var err = p.StandardError.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0)
            throw new InvalidOperationException($"{Path.GetFileName(exe)} 执行失败: {err}");
    }

    private bool GenerateSource(double seconds = 10)
    {
        if (Ffmpeg is null) return false;
        RunTool(Ffmpeg, $"-y -f lavfi -i testsrc2=duration={seconds}:size=320x240:rate=25 " +
                        $"-f lavfi -i sine=frequency=440:duration={seconds} " +
                        $"-c:v libx264 -pix_fmt yuv420p -c:a aac -shortest \"{_source}\"");
        return true;
    }

    private static double ProbeDuration(string path)
    {
        var svc = new VideoInfoService(Ffprobe!);
        return svc.GetInfo(path).Duration;
    }

    [Fact]
    public void GetInfo_RealFile_ReturnsExpectedFields()
    {
        if (!GenerateSource()) return;

        var info = new VideoInfoService(Ffprobe!).GetInfo(_source);

        Assert.Equal(320, info.Width);
        Assert.Equal(240, info.Height);
        Assert.Equal("h264", info.VideoCodec);
        Assert.Equal("aac", info.AudioCodec);
        Assert.Equal(25, info.FrameRate, 1);
        Assert.InRange(info.Duration, 9.5, 10.5);
        Assert.True(info.HasAudio);
        Assert.True(info.BitRate > 0);
    }

    [Fact]
    public async Task Cut_Copy_ProducesValidSegmentWithProgress()
    {
        if (!GenerateSource()) return;

        var cut = new CutService(Ffmpeg!, Ffprobe!);
        var outPath = Path.Combine(_dir, "copy.mp4");
        var progress = new ConcurrentQueue<double>();

        var result = await cut.CutAsync(new CutJob
        {
            InputPath = _source,
            OutputPath = outPath,
            Start = TimeSpan.FromSeconds(2),
            End = TimeSpan.FromSeconds(6),
            Profile = EncodeProfile.Copy,
        }, new Progress<CutProgress>(p => progress.Enqueue(p.Percent)), CancellationToken.None);

        Assert.True(result.Success, result.Error);
        Assert.True(File.Exists(outPath));
        var duration = ProbeDuration(outPath);
        Assert.InRange(duration, 3.5, 4.5);
        Assert.True(progress.Max() > 90, $"进度未达 90%: {progress.Max():0.0}");
    }

    [Fact]
    public async Task Cut_Reencode_ProducesHevcOutput()
    {
        if (!GenerateSource()) return;

        var cut = new CutService(Ffmpeg!, Ffprobe!);
        var outPath = Path.Combine(_dir, "hevc.mp4");
        var profile = EncodeProfile.All.First(p => p.VideoCodec == "libx265");

        var result = await cut.CutAsync(new CutJob
        {
            InputPath = _source,
            OutputPath = outPath,
            Start = TimeSpan.FromSeconds(2),
            End = TimeSpan.FromSeconds(6),
            Profile = profile,
        }, null, CancellationToken.None);

        Assert.True(result.Success, result.Error);
        var info = new VideoInfoService(Ffprobe!).GetInfo(outPath);
        Assert.Equal("hevc", info.VideoCodec);
        Assert.InRange(info.Duration, 3.5, 4.5);
    }

    [Fact]
    public async Task Cut_Cancel_StopsAndCleansUpPartialOutput()
    {
        if (!GenerateSource(30)) return;

        var cut = new CutService(Ffmpeg!, Ffprobe!);
        var outPath = Path.Combine(_dir, "cancelled.mp4");
        var profile = EncodeProfile.All.First(p => p.VideoCodec == "libx265");

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(400);

        var result = await cut.CutAsync(new CutJob
        {
            InputPath = _source,
            OutputPath = outPath,
            Start = TimeSpan.Zero,
            End = TimeSpan.FromSeconds(30),
            Profile = profile,
        }, null, cts.Token);

        Assert.False(result.Success);
        Assert.Contains("取消", result.Error);
        Assert.False(File.Exists(outPath), "取消后应删除不完整输出文件");
    }

    [Fact]
    public async Task Cut_InvalidRange_FailsWithError()
    {
        if (!GenerateSource(5)) return;

        var cut = new CutService(Ffmpeg!, Ffprobe!);
        var outPath = Path.Combine(_dir, "invalid.mp4");

        var result = await cut.CutAsync(new CutJob
        {
            InputPath = _source,
            OutputPath = outPath,
            Start = TimeSpan.FromSeconds(4),
            End = TimeSpan.FromSeconds(2),
            Profile = EncodeProfile.Copy,
        }, null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.True(result.Error.Length > 0);
    }

    [Fact]
    public void GetAvailableEncoders_ContainsCommonEncoders()
    {
        if (Ffmpeg is null || Ffprobe is null) return;

        var cut = new CutService(Ffmpeg, Ffprobe);
        var encoders = cut.GetAvailableVideoEncoders();

        Assert.Contains("libx264", encoders);
        Assert.Contains("libx265", encoders);
    }
}
