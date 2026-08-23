using System.Globalization;

namespace FFmpegVideoCutter.Services;

public sealed class ProgressParser
{
    private TimeSpan _position;
    private bool _finished;

    public TimeSpan Position => _position;
    public bool Finished => _finished;

    public void FeedLine(string line)
    {
        var idx = line.IndexOf('=');
        if (idx <= 0) return;

        var key = line[..idx].Trim();
        var value = line[(idx + 1)..].Trim();

        switch (key)
        {
            case "out_time":
                if (TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var ts))
                    _position = ts;
                break;

            case "out_time_us":
            case "out_time_ms":
                if (long.TryParse(value, out var us))
                    _position = TimeSpan.FromMilliseconds(us / 1000.0);
                break;

            case "progress":
                if (value == "end") _finished = true;
                break;
        }
    }
}
