using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace VManager.Services;

public static class YtDlpManager
{
    public static string YtDlpPath { get; private set; } = string.Empty;

    public static async Task Initialize()
    {
        string targetFile = OperatingSystem.IsWindows() ? "yt-dlp.exe"
                          : OperatingSystem.IsMacOS() ? "yt-dlp_macos"
                          : "yt-dlp";

        YtDlpPath = Path.Combine(Path.GetTempPath(), targetFile);

        if (File.Exists(YtDlpPath) && !OperatingSystem.IsWindows())
            Process.Start("chmod", $"+x \"{YtDlpPath}\"")?.WaitForExit();

        bool needsExtract = !File.Exists(YtDlpPath) || !await TestYtDlpAsync();

        if (needsExtract)
        {
            Console.WriteLine("[YTDLP] Extrayendo versión embebida…");
            ExtractForOS(targetFile);
        }

        Console.WriteLine($"[YTDLP] path: {YtDlpPath}");

        _ = TryAutoUpdateAsync();
    }

    private static void ExtractForOS(string targetFile)
    {
        if (OperatingSystem.IsWindows())
            ExtractBinary("VManager.Binaries.Windows.yt-dlp.exe", targetFile);
        else if (OperatingSystem.IsLinux())
            ExtractBinary("VManager.Binaries.Linux.yt-dlp", targetFile);
        else if (OperatingSystem.IsMacOS())
            ExtractBinary("VManager.Binaries.Mac.yt-dlp_macos", targetFile);

        if (!OperatingSystem.IsWindows())
            Process.Start("chmod", $"+x \"{YtDlpPath}\"")?.WaitForExit();
    }

    private static async Task<bool> TestYtDlpAsync()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = YtDlpPath,
                Arguments = "--version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi);
            if (p == null) return false;

            await p.WaitForExitAsync();
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static void ExtractBinary(string resourceName, string targetFile)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new Exception($"Recurso {resourceName} no encontrado");

        using var fs = new FileStream(Path.Combine(Path.GetTempPath(), targetFile), FileMode.Create, FileAccess.Write);
        stream.CopyTo(fs);
    }

    private static async Task TryAutoUpdateAsync()
    {
        var lockFilePath = Path.Combine(Path.GetTempPath(), "yt-dlp_update.lock");

        try
        {
            using var lockStream = new FileStream(lockFilePath, FileMode.CreateNew);

            var psi = new ProcessStartInfo
            {
                FileName = YtDlpPath,
                Arguments = "-U",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var p = Process.Start(psi);
            if (p == null) return;

            await p.WaitForExitAsync();
        }
        catch (IOException)
        {
            Console.WriteLine("[YTDLP] Otro proceso está actualizando.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("[YTDLP] ERROR: " + ex.Message);
            ErrorService.Show(ex);
        }
        finally
        {
            try { File.Delete(lockFilePath); } catch { }
        }
    }
}
