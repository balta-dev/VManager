using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FFMpegCore;

namespace VManager.Services;

public static class FFmpegManager
{
    public static string FfmpegPath { get; private set; } = string.Empty;
    public static string FfprobePath { get; private set; } = string.Empty;

    public static async Task Initialize()
    {
        if (!await TryUseSystemFFmpeg())
        {
            await UseEmbeddedFFmpeg();
        }

        Console.WriteLine($"[FFMPEG] ffmpeg: {FfmpegPath}");
        Console.WriteLine($"[FFMPEG] ffprobe: {FfprobePath}");

        GlobalFFOptions.Configure(new FFOptions
        {
            BinaryFolder = Path.GetDirectoryName(FfmpegPath)!
        });
    }

    // =====================================================

    private static async Task<bool> TryUseSystemFFmpeg()
    {
        string? ffmpeg = FindOnPath(OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg");
        string? ffprobe = FindOnPath(OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe");

        if (ffmpeg == null || ffprobe == null)
            return false;

        if (!await TestBinary(ffmpeg) || !await TestBinary(ffprobe))
            return false;

        FfmpegPath = ffmpeg;
        FfprobePath = ffprobe;
        return true;
    }

    private static async Task UseEmbeddedFFmpeg()
    {
        string ffmpegTarget = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
        string ffprobeTarget = OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe";

        FfmpegPath = ExtractFFmpeg(GetFFmpegResourceName(), ffmpegTarget);
        FfprobePath = ExtractFFmpeg(GetFFprobeResourceName(), ffprobeTarget);

        if (!OperatingSystem.IsWindows())
            Process.Start("chmod", $"+x \"{FfmpegPath}\"")?.WaitForExit();

        if (!await TestBinary(FfmpegPath) || !await TestBinary(FfprobePath))
            throw new Exception("Los binarios de FFmpeg no son válidos.");
    }

    // =====================================================

    private static string? FindOnPath(string name)
    {
        var envPath = Environment.GetEnvironmentVariable("PATH");
        if (envPath == null)
            return null;

        foreach (var p in envPath.Split(Path.PathSeparator))
        {
            try
            {
                var full = Path.Combine(p.Trim(), name);
                if (File.Exists(full))
                    return full;
            }
            catch { }
        }

        return null;
    }

    private static async Task<bool> TestBinary(string path)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = path,
                Arguments = "-version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi);
            if (p == null)
                return false;

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

            try
            {
                await p.WaitForExitAsync(cts.Token);
            }
            catch (OperationCanceledException)
            {
                try { p.Kill(); } catch { }
                return false;
            }

            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    // =====================================================

    private static string ExtractFFmpeg(string resourceName, string targetFileName)
    {
        string tempPath = Path.Combine(Path.GetTempPath(), targetFileName);

        if (File.Exists(tempPath))
            return tempPath;

        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new Exception($"Recurso {resourceName} no encontrado");

        using var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write);
        stream.CopyTo(fs);

        return tempPath;
    }

    private static string GetFFmpegResourceName()
    {
        if (OperatingSystem.IsWindows())
            return "VManager.Binaries.Windows.ffmpeg.exe";
        if (OperatingSystem.IsLinux())
            return "VManager.Binaries.Linux.ffmpeg";
        if (OperatingSystem.IsMacOS())
            return "VManager.Binaries.Mac.ffmpeg";

        throw new PlatformNotSupportedException();
    }

    private static string GetFFprobeResourceName()
    {
        if (OperatingSystem.IsWindows())
            return "VManager.Binaries.Windows.ffprobe.exe";
        if (OperatingSystem.IsLinux())
            return "VManager.Binaries.Linux.ffprobe";
        if (OperatingSystem.IsMacOS())
            return "VManager.Binaries.Mac.ffprobe";

        throw new PlatformNotSupportedException();
    }
}
