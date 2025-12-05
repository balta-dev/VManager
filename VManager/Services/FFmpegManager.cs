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
            InitializeWindows();
        }
        else if (OperatingSystem.IsMacOS())
        {
            InitializeMacOS();
        }
        else if (OperatingSystem.IsLinux())
        {
            InitializeLinux();
        }

        Console.WriteLine($"[DEBUG] ffmpeg: {FfmpegPath}");
        Console.WriteLine($"[DEBUG] ffprobe: {FfprobePath}");
        
        // Configura FFMpegCore con la carpeta temporal donde se extraen los ejecutables
        GlobalFFOptions.Configure(new FFOptions { BinaryFolder = Path.GetTempPath() });
    }
    
    private static void InitializeWindows()
    {
        FfmpegPath = ExtractFFmpeg("VManager.Binaries.Windows.ffmpeg.exe", "ffmpeg.exe");
        FfprobePath = ExtractFFmpeg("VManager.Binaries.Windows.ffprobe.exe", "ffprobe.exe");
    }
    
    private static void InitializeLinux()
    {
        string ffmpeg, ffprobe;
        if (!TryUseSystemFFmpeg(out ffmpeg, out ffprobe))
        {
            FfmpegPath = ExtractFFmpeg("VManager.Binaries.Linux.ffmpeg", "ffmpeg");
            FfprobePath = ExtractFFmpeg("VManager.Binaries.Linux.ffprobe", "ffprobe");

            if (!AreFFmpegBinariesCompatible(FfmpegPath, FfprobePath))
            {
                throw new Exception("Los binarios de FFmpeg extraídos están desactualizados. Avisale al dev :) o instala ffmpeg manualmente");
            }
        }
        FfmpegPath = ffmpeg;
        FfprobePath = ffprobe;
    }
    
    private static void InitializeMacOS()
    {
        string ffmpeg, ffprobe;
        if (!TryUseSystemFFmpeg(out ffmpeg, out ffprobe))
        {
            FfmpegPath = ExtractFFmpeg("VManager.Binaries.Mac.ffmpeg", "ffmpeg");
            FfprobePath = ExtractFFmpeg("VManager.Binaries.Mac.ffprobe", "ffprobe");

            if (!AreFFmpegBinariesCompatible(FfmpegPath, FfprobePath))
            {
                throw new Exception("Los binarios de FFmpeg extraídos están desactualizados. Avisale al dev :) o instala ffmpeg manualmente");
            }
        }
        FfmpegPath = ffmpeg;
        FfprobePath = ffprobe;
    }
    
    private static bool AreFFmpegBinariesCompatible(string ffmpegPath, string ffprobePath)
    {
        bool CheckVersion(string path)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = path,
                        Arguments = "-version",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                process.WaitForExit(3000);
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        return CheckVersion(ffmpegPath) && CheckVersion(ffprobePath);
    }

    private static string ExtractFFmpeg(string resourceName, string targetFileName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName)
                           ?? throw new Exception($"Recurso {resourceName} no encontrado");

        string tempPath = Path.Combine(Path.GetTempPath(), targetFileName);
        
        var dir = Path.GetDirectoryName(tempPath); //por si no existe la carpeta Temp
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        
        using var fs = File.Create(tempPath);
        stream.CopyTo(fs);

        if (!OperatingSystem.IsWindows())
            Process.Start("chmod", $"+x {tempPath}")?.WaitForExit();

        return tempPath;
    }
    
    private static bool TryUseSystemFFmpeg(out string ffmpegPath, out string ffprobePath)
    {
        ffmpegPath = null;
        ffprobePath = null;

        bool IsCommandAvailable(string command)
        {
            try
            {
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = command,
                        Arguments = "-version",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };
                process.Start();
                process.WaitForExit(2000); // Timeout de 2 segundos
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        if (IsCommandAvailable("ffmpeg") && IsCommandAvailable("ffprobe"))
        {
            ffmpegPath = "ffmpeg";
            ffprobePath = "ffprobe";
            return true;
        }

        return false;
    }

}