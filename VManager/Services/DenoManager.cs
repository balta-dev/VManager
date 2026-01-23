using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace VManager.Services;

public static class DenoManager
{
    public static string DenoPath { get; private set; } = string.Empty;

    public static void Initialize()
    {
        string targetFile = OperatingSystem.IsWindows() ? "deno.exe"
                          : OperatingSystem.IsMacOS() ? "deno_macos"
                          : "deno";

        DenoPath = Path.Combine(Path.GetTempPath(), targetFile);

        if (!File.Exists(DenoPath))
        {
            Console.WriteLine("[DENO] Extrayendo versión embebida inicial…");

            if (OperatingSystem.IsWindows())
                ExtractBinary("VManager.Binaries.Windows.deno.exe", targetFile);
            else if (OperatingSystem.IsLinux())
                ExtractBinary("VManager.Binaries.Linux.deno", targetFile);
            else if (OperatingSystem.IsMacOS())
                ExtractBinary("VManager.Binaries.Mac.deno", targetFile);

            if (!OperatingSystem.IsWindows())
                Process.Start("chmod", $"+x {DenoPath}")?.WaitForExit();
        }

        Console.WriteLine($"[DEBUG] deno path: {DenoPath}");

        _ = TryAutoUpdateAsync();
    }

    private static void ExtractBinary(string resourceName, string targetFile)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(resourceName)
                           ?? throw new Exception($"Recurso {resourceName} no encontrado");

        using var fs = File.Create(Path.Combine(Path.GetTempPath(), targetFile));
        stream.CopyTo(fs);
    }

    private static async Task TryAutoUpdateAsync()
    {
        var lockFilePath = Path.Combine(Path.GetTempPath(), "deno_update.lock");

        try
        {
            if (File.Exists(lockFilePath))
            {
                Console.WriteLine("[DENO] Otro proceso está actualizando.");
                return;
            }

            File.Create(lockFilePath).Dispose();

            try
            {
                Console.WriteLine("[DENO] Intentando actualización automática…");

                var psi = new ProcessStartInfo
                {
                    FileName = DenoPath,
                    Arguments = "upgrade",
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

                    if (!string.IsNullOrEmpty(stdout)) Console.WriteLine("[DENO] stdout: " + stdout);
                    if (!string.IsNullOrEmpty(stderr)) Console.WriteLine("[DENO] stderr: " + stderr);

                    Console.WriteLine(p.ExitCode == 0
                        ? "[DENO] Actualización completada."
                        : "[DENO] Falló la actualización, se usa versión embebida.");
                }
            }
            finally
            {
                File.Delete(lockFilePath);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine("[DENO] ERROR update: " + ex.Message);
            ErrorService.Show(ex);
        }
    }
}
