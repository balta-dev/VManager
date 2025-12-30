using System;
using System.Collections.Generic;
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
using VManager.Services;

namespace Updater
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
            this.GetObservable(ActualThemeVariantProperty).Subscribe(_ => ApplyCustomTheme());
        }
        
        private static string CacheFilePath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VManager", "cache", "update_cache.json");
        
        private static readonly string ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VManager",
            "config.json");
    
        private static readonly Dictionary<string, string> LanguageMap =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ["Español"] = "es",
                ["English"] = "en",
                ["Русский"] = "ru",
                ["Português"] = "pt",
                ["Français"] = "fr",
                ["日本語"] = "ja",
                ["中文"] = "zh",
                ["العربية"] = "ar",
                ["Deutsch"] = "de",
                ["Italiano"] = "it",
                ["한국어"] = "ko",
                ["हिंदी"] = "hi",
                ["Polski"] = "pl",
                ["Українська"] = "uk"
            };
        
        private sealed class UpdaterConfig
        {
            public string? Language { get; set; } //dto para leer idioma
        }
        
        private static string ResolveLanguageCode()
        {
            if (!File.Exists(ConfigPath))
                return "es";

            try
            {
                var json = File.ReadAllText(ConfigPath);
                var config = JsonSerializer.Deserialize<UpdaterConfig>(json);

                if (config?.Language == null)
                    return "es";

                return LanguageMap.TryGetValue(config.Language, out var code)
                    ? code
                    : "es";
            }
            catch
            {
                return "es";
            }
        }
        public static string CurrentLanguageCode => ResolveLanguageCode();
        
        public static class UpdaterLocalization
        {
            private static readonly Dictionary<string, Dictionary<string, string>> Strings =
                new()
                {
                    ["es"] = new()
                    {
                        ["Title"] = "Actualización de VManager",
                        ["CheckingUpdates"] = "Comprobando si hay actualizaciones disponibles...",
                        ["DownloadLatest"] = "Descargar última versión",
                        ["NoUpdate"] = "No hay actualización disponible.",
                        ["Downloading"] = "Descargando...",
                        ["IncompatibleAsset"] = "Error: Asset incompatible con la plataforma.",
                        ["NewVersion"] = "¡Nueva actualización disponible {0}!"
                    },
                    ["en"] = new()
                    {
                        ["Title"] = "VManager Update",
                        ["CheckingUpdates"] = "Checking for available updates...",
                        ["DownloadLatest"] = "Download latest version",
                        ["NoUpdate"] = "No update available.",
                        ["Downloading"] = "Downloading...",
                        ["IncompatibleAsset"] = "Error: incompatible asset for this platform.",
                        ["NewVersion"] = "New update available {0}!"
                    },
                    ["ru"] = new()
                    {
                        ["Title"] = "Обновление VManager",
                        ["CheckingUpdates"] = "Проверка доступных обновлений...",
                        ["DownloadLatest"] = "Скачать последнюю версию",
                        ["NoUpdate"] = "Обновление недоступно.",
                        ["Downloading"] = "Загрузка...",
                        ["IncompatibleAsset"] = "Ошибка: несовместимый файл для платформы.",
                        ["NewVersion"] = "Доступно новое обновление {0}!"
                    },
                    ["pt"] = new()
                    {
                        ["Title"] = "Atualização do VManager",
                        ["CheckingUpdates"] = "Verificando atualizações disponíveis...",
                        ["DownloadLatest"] = "Baixar versão mais recente",
                        ["NoUpdate"] = "Nenhuma atualização disponível.",
                        ["Downloading"] = "Baixando...",
                        ["IncompatibleAsset"] = "Erro: arquivo incompatível com a plataforma.",
                        ["NewVersion"] = "Nova atualização disponível {0}!"
                    },
                    ["fr"] = new()
                    {
                        ["Title"] = "Mise à jour de VManager",
                        ["CheckingUpdates"] = "Vérification des mises à jour disponibles...",
                        ["DownloadLatest"] = "Télécharger la dernière version",
                        ["NoUpdate"] = "Aucune mise à jour disponible.",
                        ["Downloading"] = "Téléchargement...",
                        ["IncompatibleAsset"] = "Erreur : fichier incompatible avec la plateforme.",
                        ["NewVersion"] = "Nouvelle mise à jour disponible {0} !"
                    },
                    ["ja"] = new()
                    {
                        ["Title"] = "VManager の更新",
                        ["CheckingUpdates"] = "更新を確認しています...",
                        ["DownloadLatest"] = "最新バージョンをダウンロード",
                        ["NoUpdate"] = "利用可能な更新はありません。",
                        ["Downloading"] = "ダウンロード中...",
                        ["IncompatibleAsset"] = "エラー: このプラットフォームと互換性のないファイルです。",
                        ["NewVersion"] = "新しいアップデート {0} が利用可能です！"
                    },
                    ["zh"] = new()
                    {
                        ["Title"] = "VManager 更新",
                        ["CheckingUpdates"] = "正在检查可用更新…",
                        ["DownloadLatest"] = "下载最新版本",
                        ["NoUpdate"] = "没有可用的更新。",
                        ["Downloading"] = "正在下载...",
                        ["IncompatibleAsset"] = "错误：与平台不兼容的文件。",
                        ["NewVersion"] = "发现新版本 {0}！"
                    },
                    ["ar"] = new()
                    {
                        ["Title"] = "تحديث VManager",
                        ["CheckingUpdates"] = "جارٍ التحقق من التحديثات المتاحة...",
                        ["DownloadLatest"] = "تنزيل أحدث إصدار",
                        ["NoUpdate"] = "لا توجد تحديثات متاحة.",
                        ["Downloading"] = "جارٍ التنزيل...",
                        ["IncompatibleAsset"] = "خطأ: ملف غير متوافق مع النظام.",
                        ["NewVersion"] = "تحديث جديد متاح {0}!"
                    },
                    ["de"] = new()
                    {
                        ["Title"] = "VManager-Update",
                        ["CheckingUpdates"] = "Suche nach verfügbaren Updates...",
                        ["DownloadLatest"] = "Neueste Version herunterladen",
                        ["NoUpdate"] = "Kein Update verfügbar.",
                        ["Downloading"] = "Wird heruntergeladen...",
                        ["IncompatibleAsset"] = "Fehler: Inkompatible Datei für diese Plattform.",
                        ["NewVersion"] = "Neues Update verfügbar: {0}!"
                    },
                    ["it"] = new()
                    {
                        ["Title"] = "Aggiornamento VManager",
                        ["CheckingUpdates"] = "Controllo degli aggiornamenti disponibili...",
                        ["DownloadLatest"] = "Scarica l'ultima versione",
                        ["NoUpdate"] = "Nessun aggiornamento disponibile.",
                        ["Downloading"] = "Download in corso...",
                        ["IncompatibleAsset"] = "Errore: file incompatibile con la piattaforma.",
                        ["NewVersion"] = "Nuovo aggiornamento disponibile {0}!"
                    },
                    ["ko"] = new()
                    {
                        ["Title"] = "VManager 업데이트",
                        ["CheckingUpdates"] = "사용 가능한 업데이트 확인 중...",
                        ["DownloadLatest"] = "최신 버전 다운로드",
                        ["NoUpdate"] = "사용 가능한 업데이트가 없습니다.",
                        ["Downloading"] = "다운로드 중...",
                        ["IncompatibleAsset"] = "오류: 플랫폼과 호환되지 않는 파일입니다.",
                        ["NewVersion"] = "새로운 업데이트 {0}이(가) 사용 가능합니다!"
                    },
                    ["hi"] = new()
                    {
                        ["Title"] = "VManager अपडेट",
                        ["CheckingUpdates"] = "उपलब्ध अपडेट की जाँच की जा रही है...",
                        ["DownloadLatest"] = "नवीनतम संस्करण डाउनलोड करें",
                        ["NoUpdate"] = "कोई अपडेट उपलब्ध नहीं है।",
                        ["Downloading"] = "डाउनलोड हो रहा है...",
                        ["IncompatibleAsset"] = "त्रुटि: इस प्लेटफ़ॉर्म के लिए असंगत फ़ाइल।",
                        ["NewVersion"] = "नया अपडेट उपलब्ध है {0}!"
                    },
                    ["pl"] = new()
                    {
                        ["Title"] = "Aktualizacja VManager",
                        ["CheckingUpdates"] = "Sprawdzanie dostępnych aktualizacji...",
                        ["DownloadLatest"] = "Pobierz najnowszą wersję",
                        ["NoUpdate"] = "Brak dostępnej aktualizacji.",
                        ["Downloading"] = "Pobieranie...",
                        ["IncompatibleAsset"] = "Błąd: niekompatybilny plik dla tej platformy.",
                        ["NewVersion"] = "Dostępna nowa aktualizacja {0}!"
                    },
                    ["uk"] = new()
                    {
                        ["Title"] = "Оновлення VManager",
                        ["CheckingUpdates"] = "Перевірка доступних оновлень...",
                        ["DownloadLatest"] = "Завантажити останню версію",
                        ["NoUpdate"] = "Оновлення недоступне.",
                        ["Downloading"] = "Завантаження...",
                        ["IncompatibleAsset"] = "Помилка: несумісний файл для цієї платформи.",
                        ["NewVersion"] = "Доступне нове оновлення {0}!"
                    }
                };
            
            public static string T(string key, params object[] args)
            {
                var lang = CurrentLanguageCode;

                if (!Strings.TryGetValue(lang, out var dict))
                    dict = Strings["es"];

                if (!dict.TryGetValue(key, out var value))
                    return key;

                return args.Length > 0
                    ? string.Format(value, args)
                    : value;
            }
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
                    Title = UpdaterLocalization.T("Title")
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

        private async Task CheckUpdatesAsync(Window window)
        {
            var update = await CheckForUpdateAsync();
            
            if (update == null || !update.UpdateAvailable || update.CurrentVersion >= update.LatestVersion)
            {
                window.Content = new TextBlock
                {
                    Text = UpdaterLocalization.T("NoUpdate"),
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
                    versionText.Text = UpdaterLocalization.T("NewVersion", update.LatestVersion);
                
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
                        downloadButton.Content = UpdaterLocalization.T("Downloading");
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
                progressText.Text = UpdaterLocalization.T("IncompatibleAsset");
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