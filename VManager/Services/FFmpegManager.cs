using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using FFMpegCore;

namespace VManager.Services;

public static class FFmpegManager
{
    public static string FfmpegPath { get; private set; } = string.Empty;
    public static string FfprobePath { get; private set; } = string.Empty;
    
    public static void Initialize()
    {
        if (OperatingSystem.IsWindows())
        {
            FfmpegPath = ExtractFFmpeg("VManager.Binaries.Windows.ffmpeg.exe", "ffmpeg.exe");
            FfprobePath = ExtractFFmpeg("VManager.Binaries.Windows.ffprobe.exe", "ffprobe.exe");
        }
        else if (OperatingSystem.IsLinux())
        {
            FfmpegPath = ExtractFFmpeg("VManager.Binaries.Linux.ffmpeg", "ffmpeg");
            FfprobePath = ExtractFFmpeg("VManager.Binaries.Linux.ffprobe", "ffprobe");
        }
        else if (OperatingSystem.IsMacOS())
        {
            FfmpegPath = ExtractFFmpeg("VManager.Binaries.Mac.ffmpeg", "ffmpeg");
            FfprobePath = ExtractFFmpeg("VManager.Binaries.Mac.ffprobe", "ffprobe");
        }
        
        // Configura FFMpegCore con la carpeta temporal donde se extraen los ejecutables
        GlobalFFOptions.Configure(new FFOptions { BinaryFolder = Path.GetTempPath() });
        
        Console.WriteLine($"[DEBUG] ffmpeg: {FfmpegPath}");
        Console.WriteLine($"[DEBUG] ffprobe: {FfprobePath}");
        
    }

    private static string ExtractFFmpeg(string resourceName, string targetFileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName)
                           ?? throw new Exception($"Recurso {resourceName} no encontrado");

        string tempPath = Path.Combine(Path.GetTempPath(), targetFileName);
        using var fs = File.Create(tempPath);
        stream.CopyTo(fs);

        if (!OperatingSystem.IsWindows())
            Process.Start("chmod", $"+x {tempPath}")?.WaitForExit();

        return tempPath;
    }
}