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
        var lockFilePath = Path.Combine(Path.GetTempPath(), "yt-dlp_update.lock");

        try
        {
            // Verificar si ya existe el archivo de lock
            if (File.Exists(lockFilePath))
            {
                Console.WriteLine("[YTDLP] Otro proceso está actualizando yt-dlp.");
                return;
            }

            // Crear el archivo de lock para evitar que otro proceso lo ejecute
            File.Create(lockFilePath).Dispose(); // Crea el archivo y lo cierra

            try
            {
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

                using var p = Process.Start(psi);
                if (p != null)
                {
                    string stdout = await p.StandardOutput.ReadToEndAsync();
                    string stderr = await p.StandardError.ReadToEndAsync();
                    await p.WaitForExitAsync();

                    if (!string.IsNullOrEmpty(stdout)) Console.WriteLine("[YTDLP] stdout: " + stdout);
                    if (!string.IsNullOrEmpty(stderr)) Console.WriteLine("[YTDLP] stderr: " + stderr);

                    Console.WriteLine(p.ExitCode == 0
                        ? "[YTDLP] Actualización completada o ya actualizado."
                        : "[YTDLP] Falló la actualización, se usa versión embebida.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("[YTDLP] ERROR en auto-update: " + ex.Message);
            }
            finally
            {
                // Eliminar el archivo de lock al finalizar
                File.Delete(lockFilePath);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("[YTDLP] ERROR al intentar crear el archivo de lock: " + ex.Message);
        }
    }

}
