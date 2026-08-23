using System.Diagnostics;
using System.Text;
using FFmpegVideoCutter.Models;
using FFmpegVideoCutter.Utils;

namespace FFmpegVideoCutter.Services;

public sealed class FFmpegService
{
    private readonly string _ffmpegPath;

    public FFmpegService(string ffmpegPath) => _ffmpegPath = ffmpegPath;

    public static string BuildArguments(CutJob job)
    {
        var sb = new StringBuilder();
        sb.Append("-y ");
        sb.Append($"-i \"{job.InputPath}\" ");
        sb.Append($"-ss {TimeHelper.ToFfmpegTime(job.Start)} ");
        sb.Append($"-to {TimeHelper.ToFfmpegTime(job.End)} ");
        sb.Append("-map 0:v:0 ");

        if (job.Profile.IsCopy)
        {
            sb.Append("-map 0:a:0? -c copy ");
        }
        else
        {
            sb.Append("-map 0:a:0? ");
            sb.Append($"-c:v {job.Profile.VideoCodec} ");
            foreach (var a in job.Profile.ExtraVideoArgs)
                sb.Append(a).Append(' ');
            sb.Append("-c:a aac -b:a 128k ");
        }

        sb.Append("-sn ");
        sb.Append("-progress pipe:1 -nostats ");
        sb.Append($"\"{job.OutputPath}\"");
        return sb.ToString();
    }

    public async Task<CutResult> CutAsync(CutJob job, IProgress<CutProgress>? progress, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments = BuildArguments(job),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("无法启动 ffmpeg");

        var stderr = new StringBuilder();
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null) stderr.AppendLine(e.Data);
        };
        process.BeginErrorReadLine();

        var parser = new ProgressParser();
        var totalSeconds = Math.Max(0.001, (job.End - job.Start).TotalSeconds);

        try
        {
            string? line;
            while ((line = await process.StandardOutput.ReadLineAsync(ct).ConfigureAwait(false)) is not null)
            {
                parser.FeedLine(line);
                var pct = Math.Clamp(parser.Position.TotalSeconds / totalSeconds * 100.0, 0, 100);
                progress?.Report(new CutProgress { Position = parser.Position, Percent = pct });
            }

            await process.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            TryDeleteOutput(job.OutputPath);
            return new CutResult { Success = false, ExitCode = -1, Error = "已取消" };
        }

        var ok = process.ExitCode == 0;
        return new CutResult
        {
            Success = ok,
            ExitCode = process.ExitCode,
            Error = ok ? "" : LastLines(stderr.ToString(), 20),
        };
    }

    public HashSet<string> GetAvailableVideoEncoders()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var psi = new ProcessStartInfo
        {
            FileName = _ffmpegPath,
            Arguments = "-hide_banner -encoders",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi);
        if (process is null) return result;

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        foreach (var raw in output.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0) continue;
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;
            if (parts[0].Length == 0 || parts[0][0] != 'V') continue;
            result.Add(parts[1]);
        }

        return result;
    }

    private static void TryDeleteOutput(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); } catch { }
    }

    private static string LastLines(string text, int count)
    {
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(Environment.NewLine, lines.Skip(Math.Max(0, lines.Length - count)));
    }
}
