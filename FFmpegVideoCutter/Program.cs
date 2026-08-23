using FFmpegVideoCutter.Services;
using FFmpegVideoCutter.UI;
using LibVLCSharp.Shared;

namespace FFmpegVideoCutter;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var locator = new FfmpegLocator();
        if (!locator.Locate())
        {
            MessageBox.Show(
                "未找到 FFmpeg / FFprobe。\n\n请将 ffmpeg.exe 和 ffprobe.exe 放到程序同目录，\n或安装到 C:\\ffmpeg\\bin，或加入系统 PATH。",
                "FFmpeg 视频截取工具",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        try { Core.Initialize(); }
        catch { /* 预览初始化失败不影响截取功能 */ }

        Application.Run(new MainForm(locator));
    }
}
