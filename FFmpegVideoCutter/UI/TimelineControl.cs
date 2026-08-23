using System.Drawing.Drawing2D;

namespace FFmpegVideoCutter.UI;

public sealed class TimelineControl : Control
{
    public event EventHandler<TimelineEventArgs>? PositionClicked;
    public event EventHandler<TimelineEventArgs>? RightClicked;
    public event EventHandler? SelectionChanged;

    private double _duration;
    private double _position;
    private double _start;
    private double _end;
    private DragMode _drag = DragMode.None;

    private const int HandleRadius = 7;

    public double Duration
    {
        get => _duration;
        set
        {
            _duration = Math.Max(0, value);
            if (_duration > 0)
            {
                _end = Math.Min(_end, _duration);
                _start = Math.Min(_start, _end);
            }
            Invalidate();
        }
    }

    public double Position
    {
        get => _position;
        set { _position = Math.Clamp(value, 0, _duration); Invalidate(); }
    }

    public double Start
    {
        get => _start;
        set { _start = Math.Clamp(value, 0, _end); Invalidate(); }
    }

    public double End
    {
        get => _end;
        set { _end = Math.Clamp(value, _start, _duration); Invalidate(); }
    }

    public void SetSelection(double start, double end)
    {
        _start = Math.Clamp(start, 0, _duration);
        _end = Math.Clamp(end, _start, _duration);
        Invalidate();
    }

    public TimelineControl()
    {
        SetStyle(ControlStyles.AllPaintingInWmPaint
               | ControlStyles.OptimizedDoubleBuffer
               | ControlStyles.UserPaint
               | ControlStyles.ResizeRedraw, true);
        BackColor = Color.FromArgb(40, 40, 40);
        Height = 40;
    }

    private enum DragMode { None, Start, End, Position }

    private int ToPx(double seconds) =>
        _duration <= 0 ? 0 : (int)Math.Round(seconds / _duration * ClientSize.Width);

    private double ToSeconds(int px) =>
        _duration <= 0 ? 0 : px / (double)Math.Max(1, ClientSize.Width) * _duration;

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var w = ClientSize.Width;
        var h = ClientSize.Height;
        var midY = h / 2;

        using (var track = new SolidBrush(Color.FromArgb(70, 70, 70)))
            g.FillRectangle(track, 0, midY - 3, w, 6);

        if (_duration > 0)
        {
            var sx = ToPx(_start);
            var ex = ToPx(_end);
            using (var sel = new SolidBrush(Color.FromArgb(110, 45, 120, 220)))
                g.FillRectangle(sel, sx, midY - 3, ex - sx, 6);
        }

        if (_position > 0)
        {
            using var play = new SolidBrush(Color.FromArgb(90, 90, 90));
            g.FillRectangle(play, 0, midY - 3, ToPx(_position), 6);
        }

        DrawTicks(g, w, h);

        using (var pos = new Pen(Color.White, 2))
            g.DrawLine(pos, ToPx(_position), 2, ToPx(_position), h - 2);

        DrawHandle(g, _start, Color.LimeGreen, 1);
        DrawHandle(g, _end, Color.Tomato, -1);
    }

    private void DrawTicks(Graphics g, int w, int h)
    {
        if (_duration <= 0) return;
        var pixelsPerSecond = w / _duration;
        var interval = pixelsPerSecond >= 60 ? 1.0
                     : pixelsPerSecond >= 12 ? 5.0
                     : pixelsPerSecond >= 3 ? 10.0
                     : 60.0;

        using var pen = new Pen(Color.FromArgb(150, 150, 150));
        for (double t = 0; t <= _duration; t += interval)
        {
            var x = ToPx(t);
            g.DrawLine(pen, x, h - 8, x, h - 1);
        }
    }

    private void DrawHandle(Graphics g, double time, Color color, int direction)
    {
        var x = ToPx(time);
        var midY = ClientSize.Height / 2;
        var pts = new[]
        {
            new Point(x, midY - 11),
            new Point(x + direction * 9, midY),
            new Point(x, midY + 11),
        };
        using var brush = new SolidBrush(color);
        g.FillPolygon(brush, pts);
    }

    private DragMode HitTest(Point p)
    {
        if (_duration <= 0) return DragMode.None;
        var nearStart = Math.Abs(p.X - ToPx(_start)) <= HandleRadius;
        var nearEnd = Math.Abs(p.X - ToPx(_end)) <= HandleRadius;

        if (nearStart && nearEnd)
            return _end <= _start + 0.5 ? DragMode.Start : DragMode.Start;
        if (nearStart) return DragMode.Start;
        if (nearEnd) return DragMode.End;
        return DragMode.None;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
        {
            if (_duration <= 0) return;
            _drag = HitTest(e.Location);
            if (_drag == DragMode.None)
            {
                _drag = DragMode.Position;
                _position = Math.Clamp(ToSeconds(e.X), 0, _duration);
                PositionClicked?.Invoke(this, new TimelineEventArgs(_position));
                Invalidate();
            }
            Capture = true;
        }
        else if (e.Button == MouseButtons.Right)
        {
            var t = Math.Clamp(ToSeconds(e.X), 0, _duration);
            RightClicked?.Invoke(this, new TimelineEventArgs(t));
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_duration <= 0) return;
        if (e.Button != MouseButtons.Left) return;

        switch (_drag)
        {
            case DragMode.Position:
                _position = Math.Clamp(ToSeconds(e.X), 0, _duration);
                PositionClicked?.Invoke(this, new TimelineEventArgs(_position));
                break;
            case DragMode.Start:
                _start = Math.Clamp(ToSeconds(e.X), 0, Math.Max(0, _end - 0.01));
                SelectionChanged?.Invoke(this, EventArgs.Empty);
                break;
            case DragMode.End:
                _end = Math.Clamp(ToSeconds(e.X), Math.Min(_duration, _start + 0.01), _duration);
                SelectionChanged?.Invoke(this, EventArgs.Empty);
                break;
        }
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        _drag = DragMode.None;
        Capture = false;
    }
}

public sealed class TimelineEventArgs : EventArgs
{
    public double Seconds { get; }

    public TimelineEventArgs(double seconds) => Seconds = seconds;
}
