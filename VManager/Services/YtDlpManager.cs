using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace VManager.Services;

public static class YtDlpManager
{
    public static string YtDlpPath { get; private set; } = string.Empty;

    private static readonly string MutexName = "Global\\VManager_YtDlp_Update_Mutex";

    public static void Initialize()
    {
        // 1) Elegir ruta persistente en Temp por plataforma
        string targetFile = OperatingSystem.IsWindows() ? "yt-dlp.exe"
                          : OperatingSystem.IsMacOS() ? "yt-dlp_macos"
                          : "yt-dlp";

        YtDlpPath = Path.Combine(Path.GetTempPath(), targetFile);

        // 2) Extraer binario embebido solo si no existe
        if (!File.Exists(YtDlpPath))
        {
            Console.WriteLine("[YTDLP] Extrayendo versión embebida inicial…");

            if (OperatingSystem.IsWindows())
                ExtractBinary("VManager.Binaries.Windows.yt-dlp.exe", targetFile);
            else if (OperatingSystem.IsLinux())
                ExtractBinary("VManager.Binaries.Linux.yt-dlp", targetFile);
            else if (OperatingSystem.IsMacOS())
                ExtractBinary("VManager.Binaries.Mac.yt-dlp_macos", targetFile);

            if (!OperatingSystem.IsWindows())
                Process.Start("chmod", $"+x {YtDlpPath}")?.WaitForExit();
        }

        Console.WriteLine($"[DEBUG] yt-dlp path: {YtDlpPath}");

        // 3) Intentar actualización automática (no bloquea)
        _ = TryAutoUpdateAsync();
    }

    // ==============================================
    // Extrae el binario embebido
    // ==============================================
    private static void ExtractBinary(string resourceName, string targetFile)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName)
                           ?? throw new Exception($"Recurso {resourceName} no encontrado");

        using var fs = File.Create(Path.Combine(Path.GetTempPath(), targetFile));
        stream.CopyTo(fs);
    }

    // ==============================================
    // Auto-update con mutex global
    // ==============================================
    private static async Task TryAutoUpdateAsync()
    {
        try
        {
            using Mutex mutex = new(false, MutexName, out bool created);

            if (!mutex.WaitOne(2000))
            {
                Console.WriteLine("[YTDLP] Otro proceso está actualizando yt-dlp.");
                return;
            }

            Console.WriteLine("[YTDLP] Intentando actualización automática…");

            var psi = new ProcessStartInfo
            {
                FileName = YtDlpPath,
                Arguments = "-U",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var p = Process.Start(psi);
            if (p != null)
            {
                string stdout = await p.StandardOutput.ReadToEndAsync();
                string stderr = await p.StandardError.ReadToEndAsync();
                await p.WaitForExitAsync();

                Console.WriteLine("[YTDLP] OUT: " + stdout);
                Console.WriteLine("[YTDLP] ERR: " + stderr);

                if (p.ExitCode == 0)
                    Console.WriteLine("[YTDLP] Actualización completada o ya actualizado.");
                else
                    Console.WriteLine("[YTDLP] Falló la actualización, se usa versión embebida.");
            }

            mutex.ReleaseMutex();
        }
        catch (Exception ex)
        {
            Console.WriteLine("[YTDLP] ERROR en auto-update: " + ex.Message);
        }
    }
}
