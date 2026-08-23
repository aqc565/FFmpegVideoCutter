using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using FFmpegVideoCutter.Models;

namespace FFmpegVideoCutter.Services;

public sealed class VideoInfoService
{
    private readonly string _ffprobePath;

    public VideoInfoService(string ffprobePath) => _ffprobePath = ffprobePath;

    public VideoInfo GetInfo(string path)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _ffprobePath,
            Arguments = $"-v quiet -print_format json -show_format -show_streams \"{path}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("无法启动 ffprobe");

        var json = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0 || string.IsNullOrWhiteSpace(json))
            throw new InvalidOperationException("ffprobe 解析失败");

        return Parse(json);
    }

    public static VideoInfo Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        int width = 0, height = 0, sampleRate = 0, channels = 0;
        double frameRate = 0;
        string videoCodec = "", audioCodec = "";
        long bitRate = 0;

        if (root.TryGetProperty("streams", out var streams) && streams.ValueKind == JsonValueKind.Array)
        {
            foreach (var s in streams.EnumerateArray())
            {
                var type = s.TryGetProperty("codec_type", out var t) ? t.GetString() : "";
                switch (type)
                {
                    case "video" when videoCodec.Length == 0:
                        if (IsAttachedPic(s)) break;
                        width = GetInt(s, "width");
                        height = GetInt(s, "height");
                        videoCodec = GetString(s, "codec_name");
                        frameRate = ParseFrameRate(s);
                        if (s.TryGetProperty("bit_rate", out var br))
                            bitRate = ParseLong(br.GetString());
                        break;

                    case "audio" when audioCodec.Length == 0:
                        audioCodec = GetString(s, "codec_name");
                        sampleRate = ParseInt(GetString(s, "sample_rate"));
                        if (s.TryGetProperty("channels", out var ch) && ch.ValueKind == JsonValueKind.Number)
                            channels = ch.GetInt32();
                        break;
                }
            }
        }

        if (bitRate == 0 && root.TryGetProperty("format", out var fmt) &&
            fmt.TryGetProperty("bit_rate", out var fbr))
            bitRate = ParseLong(fbr.GetString());

        double duration = 0;
        long size = 0;
        if (root.TryGetProperty("format", out var format))
        {
            if (format.TryGetProperty("duration", out var d))
                duration = ParseDouble(d.GetString());
            if (format.TryGetProperty("size", out var sz))
                size = ParseLong(sz.GetString());
        }

        return new VideoInfo
        {
            Width = width,
            Height = height,
            FrameRate = frameRate,
            VideoCodec = videoCodec,
            AudioCodec = audioCodec,
            BitRate = bitRate,
            Duration = duration,
            Size = size,
            AudioSampleRate = sampleRate,
            Channels = channels,
        };
    }

    private static bool IsAttachedPic(JsonElement s)
    {
        return s.TryGetProperty("disposition", out var disp) &&
               disp.TryGetProperty("attached_pic", out var ap) &&
               ap.ValueKind == JsonValueKind.Number && ap.GetInt32() == 1;
    }

    private static double ParseFrameRate(JsonElement s)
    {
        if (s.TryGetProperty("r_frame_rate", out var r) && r.ValueKind == JsonValueKind.String)
        {
            var parts = (r.GetString() ?? "").Split('/');
            if (parts.Length == 2 &&
                double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var num) &&
                double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var den) &&
                den != 0)
                return num / den;
        }
        return 0;
    }

    private static string GetString(JsonElement s, string name) =>
        s.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static int GetInt(JsonElement s, string name) =>
        s.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt32() : 0;

    private static int ParseInt(string? s) => int.TryParse(s, out var v) ? v : 0;
    private static long ParseLong(string? s) => long.TryParse(s, out var v) ? v : 0;
    private static double ParseDouble(string? s) =>
        double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;
}
