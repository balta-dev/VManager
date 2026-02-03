using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;

namespace VManager.Services;

public static class DenoManager
{
    public static string DenoPath { get; private set; } = string.Empty;

    public static async Task Initialize()
    {
        string targetFile = OperatingSystem.IsWindows() ? "deno.exe"
                          : OperatingSystem.IsMacOS() ? "deno_macos"
                          : "deno";

        DenoPath = Path.Combine(Path.GetTempPath(), targetFile);

        if (File.Exists(DenoPath) && !OperatingSystem.IsWindows())
            Process.Start("chmod", $"+x \"{DenoPath}\"")?.WaitForExit();

        bool needsExtract = !File.Exists(DenoPath) || !await TestDenoAsync();

        if (needsExtract)
        {
            Console.WriteLine("[DENO] Extrayendo versión embebida…");
            ExtractForOS(targetFile);
        }

        Console.WriteLine($"[DENO] path: {DenoPath}");

        _ = TryAutoUpdateAsync();
    }

    private static void ExtractForOS(string targetFile)
    {
        if (OperatingSystem.IsWindows())
            ExtractBinary("VManager.Binaries.Windows.deno.exe", targetFile);
        else if (OperatingSystem.IsLinux())
            ExtractBinary("VManager.Binaries.Linux.deno", targetFile);
        else if (OperatingSystem.IsMacOS())
            ExtractBinary("VManager.Binaries.Mac.deno", targetFile);

        if (!OperatingSystem.IsWindows())
            Process.Start("chmod", $"+x \"{DenoPath}\"")?.WaitForExit();
    }

    private static async Task<bool> TestDenoAsync()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = DenoPath,
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
        var lockFilePath = Path.Combine(Path.GetTempPath(), "deno_update.lock");

        try
        {
            using var lockStream = new FileStream(lockFilePath, FileMode.CreateNew);

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
            if (p == null) return;

            await p.WaitForExitAsync();
        }
        catch (IOException)
        {
            Console.WriteLine("[DENO] Otro proceso está actualizando.");
        }
        catch (Exception ex)
        {
            Console.WriteLine("[DENO] ERROR: " + ex.Message);
            ErrorService.Show(ex);
        }
        finally
        {
            try { File.Delete(lockFilePath); } catch { }
        }
    }
}
