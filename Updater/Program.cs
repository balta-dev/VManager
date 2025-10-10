using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using ReactiveUI;

namespace Updater
{
    class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                         .UsePlatformDetect()
                         .LogToTrace();
    }

    public class App : Application
    {
        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = new Window
                {
                    Title = "Actualización de VManager",
                    Width = 500,
                    Height = 400
                };
                desktop.MainWindow = mainWindow;

                mainWindow.Opened += async (_, _) => await CheckUpdatesAsync(mainWindow);
            }

            base.OnFrameworkInitializationCompleted();
        }

        private class UpdateInfo
        {
            public required Version CurrentVersion { get; set; }
            public required Version LatestVersion { get; set; }
            public required string DownloadUrl { get; set; }
            public required string ReleaseNotes { get; set; }
            public DateTime LastChecked { get; set; }
            public bool UpdateAvailable => LatestVersion > CurrentVersion;
        }

        private static string CacheFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VManager", "cache", "update_cache.json");

        private async Task CheckUpdatesAsync(Window window)
        {
            var update = await CheckForUpdateAsync();

            if (update == null || !update.UpdateAvailable)
            {
                window.Content = new TextBlock
                {
                    Text = "No hay actualización disponible.",
                    FontSize = 20,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                };
                return;
            }

            // Mostrar ventana de actualización
            var dialogContent = new StackPanel
            {
                Margin = new Thickness(10),
                Children =
                {
                    new TextBlock
                    {
                        Text = $"Nueva versión {update.LatestVersion} disponible.",
                        Margin = new Thickness(0,0,0,10),
                        Foreground = new SolidColorBrush(Color.Parse("#FFFFE066")),
                        FontSize = 23,
                        FontWeight = Avalonia.Media.FontWeight.Bold
                    },
                    new ScrollViewer
                    {
                        Height = 300,
                        Content = new TextBlock
                        {
                            Text = update.ReleaseNotes,
                            Opacity = 0.85,
                            TextWrapping = Avalonia.Media.TextWrapping.Wrap
                        }
                    },
                    new Button
                    {
                        Content = "Descargar última versión",
                        FontSize = 15
                    }
                }
            };
            window.Content = dialogContent;

            var button = dialogContent.Children[2] as Button;
            button!.Command = ReactiveCommand.CreateFromTask(async () =>
            {
                await DownloadAndUpdateAsync(update);
            }, outputScheduler: AvaloniaScheduler.Instance);
        }

        private async Task DownloadAndUpdateAsync(UpdateInfo update)
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "VManager_Update");
            Console.WriteLine("Creada carpeta temporal...");
            Directory.CreateDirectory(tempFolder);

            using var client = new HttpClient();
            var fileBytes = await client.GetByteArrayAsync(update.DownloadUrl);
            Console.WriteLine("Buscando link...");

            string downloadedFile;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Console.WriteLine("Descargando...");
                downloadedFile = Path.Combine(tempFolder, "update.zip");
                await File.WriteAllBytesAsync(downloadedFile, fileBytes);
                Console.WriteLine("Descomprimiendo...");
                System.IO.Compression.ZipFile.ExtractToDirectory(downloadedFile, tempFolder, overwriteFiles: true);
            }
            else
            {
                Console.WriteLine("Descargando...");
                downloadedFile = Path.Combine(tempFolder, "update.tar.gz");
                await File.WriteAllBytesAsync(downloadedFile, fileBytes);
                Console.WriteLine("Descomprimiendo...");
                var tarProcess = new ProcessStartInfo
                {
                    FileName = "tar",
                    Arguments = $"-xzf \"{downloadedFile}\" -C \"{tempFolder}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                var process = Process.Start(tarProcess);
                await process!.WaitForExitAsync();
            }
            
            // --- CERRAR VMANAGER ---
            Console.WriteLine("Matando VManager...");
            var processes = Process.GetProcessesByName("VManager");
            foreach (var p in processes)
            {
                p.Kill();
                try
                {
                    if (!p.WaitForExit(10000))
                        Console.WriteLine($"No se pudo cerrar {p.ProcessName} a tiempo");
                }
                catch { }
            }

            // --- REEMPLAZAR ARCHIVOS con retry seguro ---
            string targetDir = AppContext.BaseDirectory;
            Console.WriteLine($"Ubicación destino: {targetDir}");
            Console.WriteLine($"Ubicación origen: {tempFolder}");
            foreach (var file in Directory.GetFiles(tempFolder, "*", SearchOption.AllDirectories))
            {
                string fileName = Path.GetFileName(file);

                // Ignorar updater y cualquier DLL
                if (fileName.Equals("Updater", StringComparison.OrdinalIgnoreCase) ||
                    fileName.Equals("Updater.exe", StringComparison.OrdinalIgnoreCase) ||
                    fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                    fileName.EndsWith(".so", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine($"Saltando {fileName}");
                    continue;
                }

                string relative = Path.GetRelativePath(tempFolder, file);
                string dest = Path.Combine(targetDir, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                await RetryCopyAsync(file, dest);
            }

            // --- INICIAR VMANAGER actualizado ---
            Console.WriteLine($"$Intentando ejecutar ejecutable en {targetDir}");
            Process.Start(Path.Combine(targetDir, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "VManager.exe" : "VManager"));
            
            Console.WriteLine("Matando ventana de actualización...");
            Environment.Exit(0);
        }

        // Copia segura con retry para Linux/NTFS
        private async Task RetryCopyAsync(string source, string dest, int retries = 5, int delayMs = 500)
        {
            for (int i = 0; i < retries; i++)
            {
                try
                {
                    File.Copy(source, dest, true);
                    return;
                }
                catch (IOException ex) when (ex.Message.Contains("Text file busy"))
                {
                    await Task.Delay(delayMs);
                }
            }
            throw new IOException($"No se pudo copiar {source} a {dest} tras {retries} intentos");
        }


        private static async Task<UpdateInfo?> CheckForUpdateAsync()
        {
            try
            {
                var currentVersion = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0);

                HttpClient httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("VManager-Updater");
                var url = "https://api.github.com/repos/balta-dev/VManager/releases/latest";
                var json = await httpClient.GetStringAsync(url);

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var tagName = root.GetProperty("tag_name").GetString() ?? "0.0.0";
                if (tagName.StartsWith("v")) tagName = tagName[1..];
                var segments = tagName.Split('.');
                var latestVersion = new Version(
                    segments.Length > 0 ? int.Parse(segments[0]) : 0,
                    segments.Length > 1 ? int.Parse(segments[1]) : 0,
                    segments.Length > 2 ? int.Parse(segments[2]) : 0
                );

                var releaseNotes = root.GetProperty("body").GetString() ?? "";

                string platform = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "win-x64" :
                                  RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "linux-x64" :
                                  "osx-x64";

                string downloadUrl = "";
                foreach (var asset in root.GetProperty("assets").EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString() ?? "";
                    if (name.Contains(platform, StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? "";
                        break;
                    }
                }

                return new UpdateInfo
                {
                    CurrentVersion = currentVersion,
                    LatestVersion = latestVersion,
                    DownloadUrl = downloadUrl,
                    ReleaseNotes = releaseNotes,
                    LastChecked = DateTime.UtcNow
                };
            }
            catch
            {
                return null;
            }
        }
    }
}
