using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.ReactiveUI;
using Avalonia.Styling;
using ReactiveUI;

namespace Updater
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
            this.GetObservable(ActualThemeVariantProperty).Subscribe(_ => ApplyCustomTheme());
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var updateWindow = new UpdateWindow
                {
                    Width = 500,
                    Height = 500,
                    MinWidth = 300,
                    MinHeight = 400,
                    Title = "Actualización de VManager"
                };

                updateWindow.Opened += async (_, _) =>
                {
                    await CheckUpdatesAsync(updateWindow);
                };

                desktop.MainWindow = updateWindow;
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
            
            if (update == null || !update.UpdateAvailable || update.CurrentVersion >= update.LatestVersion)
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

            if (window is UpdateWindow updateWindow)
            {
                var versionText = updateWindow.FindControl<TextBlock>("VersionText");
                var releaseNotesText = updateWindow.FindControl<TextBlock>("ReleaseNotesText");
                var downloadButton = updateWindow.FindControl<Button>("DownloadButton");
                var progressText = updateWindow.FindControl<TextBlock>("ProgressText");
                var progressBar = updateWindow.FindControl<ProgressBar>("ProgressBar");
                var awaitUpdate = updateWindow.FindControl<TextBlock>("AwaitUpdate");
                
                if (versionText != null)
                    versionText.Text = $"¡Nueva actualización disponible {update.LatestVersion}!";
                
                if (releaseNotesText != null)
                    releaseNotesText.Text = update.ReleaseNotes;
                
                if (downloadButton != null && progressBar != null && progressText != null)
                {
                    downloadButton.IsVisible = true;
                    progressBar.IsVisible = true;
                    progressText.IsVisible = true;
                    if (awaitUpdate != null ) awaitUpdate.IsVisible = false;
                }

                if (downloadButton != null && progressText != null)
                {
                    downloadButton.Command = ReactiveCommand.CreateFromTask(async () =>
                    {
                        downloadButton.IsEnabled = false;
                        downloadButton.Content = "Descargando...";
                        downloadButton.Background = Brushes.LightGray;
                        downloadButton.Foreground = Brushes.Gray;
                        await DownloadAndUpdateAsync(update, progressText, progressBar);
                    }, outputScheduler: AvaloniaScheduler.Instance);
                }
            }
        }

        private async Task DownloadFileWithProgressAsync(string url, string destination, IProgress<double> progress)
        {
            using var client = new HttpClient();
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? -1L;
            var canReportProgress = totalBytes != -1;

            using var contentStream = await response.Content.ReadAsStreamAsync();
            using var fileStream = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            long totalRead = 0;
            int bytesRead;
            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead);
                totalRead += bytesRead;

                if (canReportProgress)
                {
                    double percent = (totalRead * 100.0) / totalBytes;
                    progress.Report(percent);
                }
            }
        }

        private async Task DownloadAndUpdateAsync(UpdateInfo update, TextBlock progressText, ProgressBar progressBar)
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), "VManager_Update");
            Directory.CreateDirectory(tempFolder);

            string extension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".zip" : ".tar.gz";
            string downloadedFile = Path.Combine(tempFolder, $"update{extension}");

            // Verifica que la URL termine con la extensión esperada
            if (!update.DownloadUrl.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                progressText.Text = "Error: Asset incompatible con la plataforma.";
                return;
            }

            var progress = new Progress<double>(p =>
            {
                progressBar.Value = p;
                progressText.Text = $"{p:F0}%";
            });

            await DownloadFileWithProgressAsync(update.DownloadUrl, downloadedFile, progress);

            // Resto del código sin cambios
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                System.IO.Compression.ZipFile.ExtractToDirectory(downloadedFile, tempFolder, overwriteFiles: true);
            }
            else
            {
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

            var processes = Process.GetProcessesByName("VManager");
            foreach (var p in processes)
            {
                p.Kill();
                try { if (!p.WaitForExit(10000)) Console.WriteLine($"No se pudo cerrar {p.ProcessName} a tiempo"); } catch { }
            }

            string targetDir = AppContext.BaseDirectory;
            foreach (var file in Directory.GetFiles(tempFolder, "*", SearchOption.AllDirectories))
            {
                string fileName = Path.GetFileName(file);
                if (fileName.Equals("Updater", StringComparison.OrdinalIgnoreCase) ||
                    fileName.Equals("Updater.exe", StringComparison.OrdinalIgnoreCase) ||
                    fileName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                    fileName.EndsWith(".so", StringComparison.OrdinalIgnoreCase) ||
                    fileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase) ||
                    fileName.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase))
                    continue;

                string relative = Path.GetRelativePath(tempFolder, file);
                string dest = Path.Combine(targetDir, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
                await RetryCopyAsync(file, dest);
            }

            Process.Start(Path.Combine(targetDir, RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "VManager.exe" : "VManager"));

            Environment.Exit(0);
        }

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

        private static bool VersionsAreEqual(Version? a, Version? b)
        {
            if (a == null || b == null) return false;
            int buildA = a.Build < 0 ? 0 : a.Build;
            int buildB = b.Build < 0 ? 0 : b.Build;
            int revA = a.Revision < 0 ? 0 : a.Revision;
            int revB = b.Revision < 0 ? 0 : b.Revision;
            return a.Major == b.Major && a.Minor == b.Minor && buildA == buildB && revA == revB;
        }

        public static class BuildInfo
        {
            public static bool IsSelfContained()
            {
                try
                {
                    var baseDir = AppContext.BaseDirectory;
                    // Verifica múltiples archivos para mayor confiabilidad
                    return File.Exists(Path.Combine(baseDir, "hostfxr.dll")) &&
                           File.Exists(Path.Combine(baseDir, "coreclr.dll"));
                }
                catch
                {
                    // Asume framework-dependent si hay error
                    return false;
                }
            }
        }
        
        private static async Task<UpdateInfo?> CheckForUpdateAsync()
        {
            if (File.Exists(CacheFilePath))
            {
                try
                {
                    var json = await File.ReadAllTextAsync(CacheFilePath);
                    var cached = JsonSerializer.Deserialize<UpdateInfo>(json);

                    if (cached != null)
                    {
                        var timeSinceCheck = DateTime.UtcNow - cached.LastChecked;
                        if (timeSinceCheck.TotalMinutes < 2 && VersionsAreEqual(cached.CurrentVersion, Assembly.GetEntryAssembly()?.GetName().Version!))
                        {
                            Console.WriteLine("Usando información en caché (menos de 2 minutos).");
                            return cached;
                        }
                    }
                }
                catch (Exception ex) { Console.WriteLine($"Error leyendo caché: {ex.Message}"); }
            }

            try
            {
                var currentVersion = Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(0, 0, 0);
                currentVersion = new Version(currentVersion.Major, currentVersion.Minor, currentVersion.Build);

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

                // Detecta si es self-contained
                bool isSelfContained = BuildInfo.IsSelfContained();
                string assetSuffix = isSelfContained ? "-self-contained" : "";
                string expectedAssetName = $"VManager-{platform}{assetSuffix}"; // Ej: "VManager-win-x64-self-contained" o "VManager-win-x64"

                string downloadUrl = "";
                foreach (var asset in root.GetProperty("assets").EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString() ?? "";
                    if (name.Contains(expectedAssetName, StringComparison.OrdinalIgnoreCase) &&
                        name.EndsWith(RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".zip" : ".tar.gz", StringComparison.OrdinalIgnoreCase))
                    {
                        downloadUrl = asset.GetProperty("browser_download_url").GetString() ?? "";
                        break;
                    }
                }

                if (string.IsNullOrEmpty(downloadUrl))
                {
                    Console.WriteLine($"No se encontró un asset compatible para {expectedAssetName}");
                    return null; // O lanza una excepción si prefieres
                }

                var updateInfo = new UpdateInfo
                {
                    CurrentVersion = currentVersion,
                    LatestVersion = latestVersion,
                    DownloadUrl = downloadUrl,
                    ReleaseNotes = releaseNotes,
                    LastChecked = DateTime.UtcNow
                };

                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(CacheFilePath)!);
                    var cacheJson = JsonSerializer.Serialize(updateInfo, new JsonSerializerOptions { WriteIndented = true });
                    await File.WriteAllTextAsync(CacheFilePath, cacheJson);
                }
                catch (Exception ex) { Console.WriteLine($"No se pudo guardar caché: {ex.Message}"); }

                return updateInfo;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error verificando actualizaciones: {ex.Message}");
                return null;
            }
        }
        
        private void ApplyCustomTheme(ThemeVariant? theme = null)
        {
            var actualTheme = theme ?? ActualThemeVariant;

            if (actualTheme == ThemeVariant.Dark)
            {
                Resources["WindowBackgroundBrush"] = Resources["WindowBackgroundBrushDark"];
                Resources["PanelBackgroundBrush"] = Resources["PanelBackgroundBrushDark"];
                Resources["BorderBrushPrimary"] = Resources["BorderBrushPrimaryDark"];
            }
            else
            {
                Resources["WindowBackgroundBrush"] = Resources["WindowBackgroundBrushLight"];
                Resources["PanelBackgroundBrush"] = Resources["PanelBackgroundBrushLight"];
                Resources["BorderBrushPrimary"] = Resources["BorderBrushPrimaryLight"];
            }
        }
    }
}