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

    // Carpeta persistente, hermana de "Themes"
    private static string AppRoot => Path.GetDirectoryName(Environment.ProcessPath!)!;
    private static string BinariesPath => Path.Combine(AppRoot, "Binaries");

    private static readonly SemaphoreSlim _extractLock = new(1, 1);

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
        Directory.CreateDirectory(BinariesPath);

        string ffmpegTarget = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
        string ffprobeTarget = OperatingSystem.IsWindows() ? "ffprobe.exe" : "ffprobe";

        FfmpegPath = await ExtractFFmpeg(GetFFmpegResourceName(), ffmpegTarget);
        FfprobePath = await ExtractFFmpeg(GetFFprobeResourceName(), ffprobeTarget);

        if (!OperatingSystem.IsWindows())
        {
            Process.Start("chmod", $"+x \"{FfmpegPath}\"")?.WaitForExit();
            Process.Start("chmod", $"+x \"{FfprobePath}\"")?.WaitForExit();
        }

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

    private static async Task<string> ExtractFFmpeg(string resourceName, string targetFileName)
    {
        string finalPath = Path.Combine(BinariesPath, targetFileName);

        if (File.Exists(finalPath))
            return finalPath;

        await _extractLock.WaitAsync();
        try
        {
            // Doble chequeo por si otro hilo ya extrajo mientras esperábamos el lock
            if (File.Exists(finalPath))
                return finalPath;

            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new Exception($"Recurso {resourceName} no encontrado");

            string tempPath = finalPath + $".tmp_{Guid.NewGuid():N}";
            using (var fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write))
            {
                await stream.CopyToAsync(fs);
            }
            File.Move(tempPath, finalPath, overwrite: true);

            return finalPath;
        }
        finally
        {
            _extractLock.Release();
        }
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