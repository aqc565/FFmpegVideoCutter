using FFmpegVideoCutter.Models;

namespace FFmpegVideoCutter.UI;

public sealed class ErrorDetailsForm : Form
{
    public ErrorDetailsForm(IReadOnlyList<CutError> errors)
    {
        Text = "错误详情";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(560, 360);
        MinimizeBox = false;
        MaximizeBox = false;
        FormBorderStyle = FormBorderStyle.Sizable;

        var text = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 9f),
        };

        var sb = new System.Text.StringBuilder();
        foreach (var e in errors)
        {
            sb.AppendLine(e.FileName);
            sb.AppendLine($"├─ 错误码: {e.ErrorCode}");
            sb.AppendLine($"├─ 时间: {e.Time:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"└─ 信息: {e.Message}");
            sb.AppendLine();
        }

        text.Text = sb.ToString();

        var close = new Button { Text = "关闭", Dock = DockStyle.Bottom, Height = 36 };
        close.Click += (_, _) => Close();

        Controls.Add(text);
        Controls.Add(close);
    }
}
