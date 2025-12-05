using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace VManager.Services;

public static class YtDlpManager
{
    public static string YtDlpPath { get; private set; } = string.Empty;

    public static void Initialize()
    {
        if (OperatingSystem.IsWindows())
        {
            YtDlpPath = ExtractBinary("VManager.Binaries.Windows.yt-dlp.exe", "yt-dlp.exe");
        }
        else if (OperatingSystem.IsLinux())
        {
            YtDlpPath = ExtractBinary("VManager.Binaries.Linux.yt-dlp", "yt-dlp");

            Process.Start("chmod", $"+x {YtDlpPath}")?.WaitForExit();
        }
        else if (OperatingSystem.IsMacOS())
        {
            YtDlpPath = ExtractBinary("VManager.Binaries.Mac.yt-dlp_macos", "yt-dlp_macos");

            Process.Start("chmod", $"+x {YtDlpPath}")?.WaitForExit();
        }

        Console.WriteLine($"[DEBUG] yt-dlp path: {YtDlpPath}");
    }

    private static string ExtractBinary(string resourceName, string targetFile)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName)
                           ?? throw new Exception($"Recurso {resourceName} no encontrado");

        string outPath = Path.Combine(Path.GetTempPath(), targetFile);
        using var fs = File.Create(outPath);
        stream.CopyTo(fs);

        return outPath;
    }
}