namespace FFmpegVideoCutter.Utils;

public static class FileHelper
{
    public static string BuildOutputName(string inputPath, TimeSpan start, TimeSpan end, string extension)
    {
        var dir = Path.GetDirectoryName(inputPath) ?? "";
        var name = Path.GetFileNameWithoutExtension(inputPath);
        var startStr = TimeHelper.Format(start).Replace(':', '-');
        var endStr = TimeHelper.Format(end).Replace(':', '-');
        var ext = extension.StartsWith('.') ? extension : "." + extension;
        return Path.Combine(dir, $"{name}_{startStr}_{endStr}{ext}");
    }

    public static bool HasEnoughFreeSpace(string directory, long requiredBytes, out long freeBytes)
    {
        freeBytes = 0;
        try
        {
            var full = Path.GetFullPath(directory);
            var root = Path.GetPathRoot(full) ?? "C:\\";
            var drive = new DriveInfo(root);
            freeBytes = drive.AvailableFreeSpace;
            return freeBytes >= requiredBytes;
        }
        catch
        {
            return true;
        }
    }

    public static string EnsureUniquePath(string path)
    {
        if (!File.Exists(path)) return path;

        var dir = Path.GetDirectoryName(path) ?? "";
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        var i = 1;
        string candidate;
        do
        {
            candidate = Path.Combine(dir, $"{name}({i}){ext}");
            i++;
        } while (File.Exists(candidate));
        return candidate;
    }
}
