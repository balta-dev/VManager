using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using VManager.Services.Core.Media;
using VManager.Services.Models;

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
        var config = ConfigurationService.Current;

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
        
            var info = JsonSerializer.Deserialize<VideoInfo>(json, VManagerJsonContext.Default.VideoInfo)!;
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
    CancellationToken cancellationToken,
    string? formatId = null)
    {
        string cookieArg = BuildCookiesArgument();
        string safeOutput = OutputPathBuilder.SanitizeFilename(outputTemplate);
        
        var psi = new ProcessStartInfo
        {
            FileName = _ytDlpPath,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        //cambiada la forma en que se construyen argumentos para evitar escapes de comillas
        if (!string.IsNullOrWhiteSpace(cookieArg))
        {
            foreach (var part in cookieArg.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                psi.ArgumentList.Add(part);
        }

        psi.ArgumentList.Add("--newline");
        
        /*
         https://github.com/yt-dlp/yt-dlp/issues/15569
         web_safari deja de poder puede descargar m3u8 porque yt empezó a forzar SABR
         el problema es que yt-dlp todavía no entiende SABR
         https://github.com/yt-dlp/yt-dlp/issues/12482
        */
        psi.ArgumentList.Add("--js-runtimes"); //agregado porque eventualmente sin este parámetro no va a funcionar
        psi.ArgumentList.Add($"deno:{DenoManager.DenoPath}"); //agregado denomanager + binarios deno para resolver challenges de js      
        psi.ArgumentList.Add("--extractor-args");
        psi.ArgumentList.Add("youtube:player_client=default,-web_safari");
        /////////////
        
        psi.ArgumentList.Add("-o");
        psi.ArgumentList.Add(safeOutput);

        if (!string.IsNullOrEmpty(formatId))
        {
            if (formatId == "0")
            {
                psi.ArgumentList.Add("-x");
                psi.ArgumentList.Add("--audio-format");
                psi.ArgumentList.Add("mp3");
            }
            else if (formatId == "1")
            {
                psi.ArgumentList.Add("-x");
                psi.ArgumentList.Add("--audio-format");
                psi.ArgumentList.Add("wav");
            }
            //removido forzado -f {formatId} (-f 91, correspondiente al protocolo m3u8)
        }

        psi.ArgumentList.Add(url);

        var process = new Process { StartInfo = psi };

        try
        {
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            // Registrar cancelación para matar el proceso
            linkedCts.Token.Register(() =>
            {
                if (!process.HasExited)
                {
                    try
                    {
                        process.Kill(entireProcessTree: true);
                        Console.WriteLine("[DEBUG] Proceso yt-dlp cancelado.");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("[DEBUG] Error matando proceso: " + ex.Message);
                    }

                    // Borrar archivo parcial
                    if (File.Exists(outputTemplate))
                    {
                        try
                        {
                            File.Delete(outputTemplate);
                            Console.WriteLine("[DEBUG] Archivo parcial eliminado tras cancelación.");
                        }
                        catch (IOException ex)
                        {
                            Console.WriteLine("[DEBUG] No se pudo eliminar archivo parcial: " + ex.Message);
                        }
                    }
                }
            });

            Console.WriteLine("[YTDLP CMD] " + psi.FileName + " " + string.Join(" ", psi.ArgumentList));
            process.Start();

            // Leer stdout y stderr con respeto al token
            Task readStdout = Task.Run(async () =>
            {
                using var r = process.StandardOutput;
                string? line;
                while ((line = await r.ReadLineAsync()) != null)
                {
                    linkedCts.Token.ThrowIfCancellationRequested();
                    ProcessYtDlpLine(line, progress);
                }
            }, linkedCts.Token);

            Task readStderr = Task.Run(async () =>
            {
                using var r = process.StandardError;
                string? line;
                while ((line = await r.ReadLineAsync()) != null)
                {
                    linkedCts.Token.ThrowIfCancellationRequested();
                    ProcessYtDlpLine(line, progress);
                }
            }, linkedCts.Token);

            await Task.WhenAll(readStdout, readStderr);
            await process.WaitForExitAsync(linkedCts.Token);

            if (linkedCts.Token.IsCancellationRequested)
            {
                return new ProcessingResult(false, "Operación cancelada por el usuario.");
            }

            if (process.ExitCode == 0)
                return new ProcessingResult(true, "Descarga completada");

            return new ProcessingResult(false, "Error ejecutando yt-dlp");
        }
        catch (OperationCanceledException)
        {
            return new ProcessingResult(false, "Operación cancelada por el usuario.");
        }
        catch (Exception ex)
        {
            return new ProcessingResult(false, $"Error: {ex.Message}");
        }
        
    }
    
}