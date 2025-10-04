using System;
using System.Diagnostics;
using System.IO;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length < 2) return;

        string targetDir = args[0];
        string sourceDir = args[1];

        // Esperar a que la app principal cierre
        var processes = Process.GetProcessesByName("VManager"); 
        foreach (var p in processes) p.WaitForExit();

        // Copiar archivos
        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(sourceDir, file);
            string dest = Path.Combine(targetDir, relative);
    
            // Saltar el updater DESTINO para no sobrescribirlo mientras corre
            string destFileName = Path.GetFileName(dest);
            if (destFileName.Equals("Updater", StringComparison.OrdinalIgnoreCase) ||
                destFileName.Equals("Updater.exe", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
    
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, overwrite: true);
        }

        // Borrar la carpeta temporal
        try
        {
            Directory.Delete(sourceDir, recursive: true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al borrar la carpeta temporal: {ex.Message}");
        }

        // Iniciar VManager
        string exeName = "VManager" + (OperatingSystem.IsWindows() ? ".exe" : "");
        Process.Start(Path.Combine(targetDir, exeName));
    }
}