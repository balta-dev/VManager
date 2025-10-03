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
    
            // Saltar el updater para no sobrescribirlo mientras corre
            if (Path.GetFileName(file).Equals("Updater", StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(file).Equals("Updater.exe", StringComparison.OrdinalIgnoreCase))
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
            // Opcional: Registrar el error en un log si es necesario
        }

        // Iniciar VManager
        string exeName = "VManager" + (OperatingSystem.IsWindows() ? ".exe" : "");
        Process.Start(Path.Combine(targetDir, exeName));
    }
}