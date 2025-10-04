using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Reflection;
using System.Threading.Tasks;

namespace VManager.Services
{
    public class UpdateChecker
    {
        private const string RepoOwner = "balta-dev";
        private const string RepoName = "VManager";
        private static readonly HttpClient httpClient = new HttpClient();
        private static readonly string CacheFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VManager", "cache", "update_cache.json");

        public class UpdateInfo
        {
            public Version CurrentVersion { get; set; }
            public Version LatestVersion { get; set; }
            public string DownloadUrl { get; set; }
            public string ReleaseNotes { get; set; }
            public DateTime LastChecked { get; set; }
            public bool UpdateAvailable => LatestVersion > CurrentVersion;
        }

        public static async Task<UpdateInfo?> CheckForUpdateAsync()
        {
            // Versión actual de la app
            var exePath = Assembly.GetEntryAssembly()?.Location ?? "";
            if (string.IsNullOrEmpty(exePath))
            {
                exePath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
            }
            var fvi = FileVersionInfo.GetVersionInfo(exePath);
            var currentVersion = new Version(fvi.ProductVersion?.Split('+')[0] ?? "0.0.0");
            
            // Intentar usar cache reciente (< 5 minutos)
            var cached = LoadCache();
            if (cached != null && cached.CurrentVersion == currentVersion && (DateTime.UtcNow - cached.LastChecked).TotalMinutes < 5)
                return cached;

            try
            {
                // Llamada a GitHub API
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("VManager-Updater");
                var url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
                var json = await httpClient.GetStringAsync(url);

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                // Versión remota
                var tagName = root.GetProperty("tag_name").GetString() ?? "0.0.0";
                if (tagName.StartsWith("v")) tagName = tagName[1..];
                var segments = tagName.Split(new[] { '+', '-' }, StringSplitOptions.RemoveEmptyEntries)[0].Split('.');
                var latestVersion = new Version(
                    segments.Length > 0 ? int.Parse(segments[0]) : 0,
                    segments.Length > 1 ? int.Parse(segments[1]) : 0,
                    segments.Length > 2 ? int.Parse(segments[2]) : 0
                );

                // Notas de la versión
                var releaseNotes = root.GetProperty("body").GetString() ?? "";

                // Asset por plataforma
                string platform = GetPlatformAssetName();
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

                var updateInfo = new UpdateInfo
                {
                    CurrentVersion = currentVersion,
                    LatestVersion = latestVersion,
                    DownloadUrl = downloadUrl,
                    ReleaseNotes = releaseNotes,
                    LastChecked = DateTime.UtcNow
                };

                SaveCache(updateInfo);
                return updateInfo;
            }
            catch
            {
                return cached; // fallback si falla
            }
        }

        private static string GetPlatformAssetName()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) return "win-x64";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) return "linux-x64";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) return "osx-x64";
            return "";
        }

        private static void SaveCache(UpdateInfo info)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(CacheFilePath)!);
                var options = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(CacheFilePath, JsonSerializer.Serialize(info, options));
            }
            catch { }
        }

        private static UpdateInfo? LoadCache()
        {
            try
            {
                if (File.Exists(CacheFilePath))
                {
                    var json = File.ReadAllText(CacheFilePath);
                    return JsonSerializer.Deserialize<UpdateInfo>(json);
                }
                return null;
            }
            catch
            {
                return null;
            }
        }
        
        public static void InvalidateCache()
        {
            try
            {
                if (File.Exists(CacheFilePath))
                    File.Delete(CacheFilePath);
            }
            catch { }
        }
    }
}