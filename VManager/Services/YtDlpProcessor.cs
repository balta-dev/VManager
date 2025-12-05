using System;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace VManager.Services;

public class YtDlpProcessor
{
    private readonly string _ytDlpPath = YtDlpManager.YtDlpPath;

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

    
    public async Task<ProcessingResult> DownloadAsync(
    string url,
    string outputTemplate,
    IProgress<YtDlpProgress> progress,
    CancellationToken cancellationToken)
    {
        var psi = new ProcessStartInfo
        {
            FileName = _ytDlpPath,
            Arguments = $"--newline -o \"{outputTemplate}\" {url}",
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
