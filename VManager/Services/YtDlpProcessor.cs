using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace VManager.Services;

public class YtDlpProcessor
{
    private readonly string _ytDlpPath = YtDlpManager.YtDlpPath;
    
    // ============================================================
    //            NUEVO: DETECTA NAVEGADOR POR SISTEMA
    // ============================================================

    private static string? DetectBrowser()
    {
        if (OperatingSystem.IsWindows())
            return DetectBrowserWindows();

        if (OperatingSystem.IsLinux())
            return DetectBrowserLinux();

        if (OperatingSystem.IsMacOS())
            return DetectBrowserMac();

        return null;
    }

    private static string? DetectBrowserWindows()
    {
        try
        {
            const string path = @"Software\Microsoft\Windows\Shell\Associations\UrlAssociations\http\UserChoice";
            var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(path);
            var progId = key?.GetValue("ProgId")?.ToString()?.ToLower();

            if (progId == null) return null;

            if (progId.Contains("chrome")) return "chrome";
            if (progId.Contains("edge")) return "edge";
            if (progId.Contains("firefox")) return "firefox";
            if (progId.Contains("brave")) return "brave";
            if (progId.Contains("opera")) return "opera";
            if (progId.Contains("vivaldi")) return "vivaldi";

            return null;
        }
        catch { return null; }
    }

    private static string? DetectBrowserLinux()
    {
        try
        {
            var result = Process.Start(new ProcessStartInfo
            {
                FileName = "xdg-settings",
                Arguments = "get default-web-browser",
                RedirectStandardOutput = true,
                UseShellExecute = false
            });

            string output = result!.StandardOutput.ReadToEnd().Trim().ToLower();

            if (output.Contains("chrome")) return "chrome";
            if (output.Contains("chromium")) return "chromium";
            if (output.Contains("firefox")) return "firefox";
            if (output.Contains("brave")) return "brave";
            if (output.Contains("opera")) return "opera";
            if (output.Contains("vivaldi")) return "vivaldi";

            return null;
        }
        catch { return null; }
    }

    private static string? DetectBrowserMac()
    {
        try
        {
            var result = Process.Start(new ProcessStartInfo
            {
                FileName = "bash",
                Arguments = "-c \"defaultbrowser\"",
                RedirectStandardOutput = true,
                UseShellExecute = false
            });

            string output = result!.StandardOutput.ReadToEnd().Trim().ToLower();

            if (output.Contains("chrome")) return "chrome";
            if (output.Contains("firefox")) return "firefox";
            if (output.Contains("safari")) return "safari";
            if (output.Contains("edge")) return "edge";
            if (output.Contains("opera")) return "opera";

            return null;
        }
        catch { return null; }
    }


    // ============================================================
    //                        PROGRESS
    // ============================================================

    public class YtDlpProgress
    {
        public double Progress { get; }
        public string Speed { get; }
        public string Eta { get; }

        public YtDlpProgress(double progress, string speed, string eta)
        {
            Progress = progress;
            Speed = speed;
            Eta = eta;
        }
    }

    private void ProcessYtDlpLine(string line, IProgress<YtDlpProgress>? progress)
    {
        Console.WriteLine("[YTDLP] " + line);
        
        if (line.Contains("Sleeping") && line.Contains("seconds"))
        {
            progress?.Report(new YtDlpProgress(
                0,
                "Esperando...",
                "Preparando..."
            ));
        }

        if (!line.StartsWith("[download]"))
            return;

        // REGEX
        var m = Regex.Match(line,
            @"(\d+(?:\.\d+)?)%.*?at\s+(\S+)\s+ETA\s+(\S+)",
            RegexOptions.IgnoreCase);

        if (m.Success)
        {
            double pct = double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) / 100;
            string speed = m.Groups[2].Value;
            string eta = m.Groups[3].Value;

            progress?.Report(new YtDlpProgress(pct, speed, eta));
            return;
        }

        var m2 = Regex.Match(line, @"(\d+(?:\.\d+)?)%");
        if (m2.Success)
        {
            double pct = double.Parse(m2.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) / 100;
            progress?.Report(new YtDlpProgress(pct, "", ""));
        }
    }
    
    private string BuildCookiesArgument()
    {
        var config = ConfigurationService.Load();

        // 1) Archivo de cookies
        if (config.UseCookiesFile && !string.IsNullOrWhiteSpace(config.CookiesFilePath))
        {
            if (File.Exists(config.CookiesFilePath))
            {
                return $"--cookies \"{config.CookiesFilePath}\"";
            }
            else
            {
                Console.WriteLine("[YTDLP] Archivo de cookies configurado pero no existe.");
            }
        }

        // 2) Cookies del navegador
        string? browser = DetectBrowser();
        if (browser != null)
            return $"--cookies-from-browser {browser}";

        // 3) Sin cookies
        return "";
    }

    // ============================================================
    //                   OBTENER INFO DEL VIDEO
    // ============================================================

    public async Task<VideoInfo?> GetVideoInfoAsync(string url, CancellationToken cancellationToken = default)
    {
        string cookieArg = BuildCookiesArgument();

        var psi = new ProcessStartInfo
        {
            FileName = _ytDlpPath,
            Arguments = $"{cookieArg} --dump-json --no-warnings \"{url}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var process = new Process { StartInfo = psi };

        try
        {
            process.Start();
        
            string json = await process.StandardOutput.ReadToEndAsync();
            string error = await process.StandardError.ReadToEndAsync();
        
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                Console.WriteLine($"Error obteniendo info: {error}");
                return null;
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        
            var info = JsonSerializer.Deserialize<VideoInfo>(json, options);
            return info;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al obtener info del video: {ex.Message}");
            return null;
        }
    }


    // ============================================================
    //                      DESCARGAR VIDEO
    // ============================================================
    
    public async Task<ProcessingResult> DownloadAsync(
        string url,
        string outputTemplate,
        IProgress<YtDlpProgress> progress,
        CancellationToken cancellationToken)
    {
        string cookieArg = BuildCookiesArgument();

        var psi = new ProcessStartInfo
        {
            FileName = _ytDlpPath,
            Arguments = $"{cookieArg} --newline -o \"{outputTemplate}\" {url}",
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        var process = new Process { StartInfo = psi };

        try
        {
            process.Start();

            Task readStdout = Task.Run(async () =>
            {
                using var r = process.StandardOutput;
                string? line;
                while ((line = await r.ReadLineAsync()) != null)
                    ProcessYtDlpLine(line, progress);
            });

            Task readStderr = Task.Run(async () =>
            {
                using var r = process.StandardError;
                string? line;
                while ((line = await r.ReadLineAsync()) != null)
                    ProcessYtDlpLine(line, progress);
            });

            await Task.WhenAll(readStdout, readStderr);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode == 0)
                return new ProcessingResult(true, "Descarga completada");

            return new ProcessingResult(false, "Error ejecutando yt-dlp");
        }
        catch (Exception ex)
        {
            return new ProcessingResult(false, $"Error: {ex.Message}");
        }
    }


}

// ============================================================
//                    CLASES DE DATOS
// ============================================================

public class VideoInfo
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
    
    [JsonPropertyName("duration")]
    public int Duration { get; set; }
    
    [JsonPropertyName("thumbnail")]
    public string Thumbnail { get; set; } = string.Empty;
    
    [JsonPropertyName("filesize")]
    public long? FileSize { get; set; }
    
    [JsonPropertyName("formats")]
    public List<FormatInfo> Formats { get; set; } = new();
}

public class FormatInfo
{
    [JsonPropertyName("format_id")]
    public string FormatId { get; set; } = string.Empty;
    
    [JsonPropertyName("ext")]
    public string Extension { get; set; } = string.Empty;
    
    [JsonPropertyName("resolution")]
    public string? Resolution { get; set; }
    
    [JsonPropertyName("height")]
    public int? Height { get; set; }
    
    [JsonPropertyName("width")]
    public int? Width { get; set; }
    
    [JsonPropertyName("filesize")]
    public long? FileSize { get; set; }
    
    [JsonPropertyName("vcodec")]
    public string VideoCodec { get; set; } = string.Empty;
    
    [JsonPropertyName("acodec")]
    public string AudioCodec { get; set; } = string.Empty;
}