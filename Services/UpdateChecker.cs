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
            Console.WriteLine("Buscando actualizaciones...");

            // 1. Ver si existe cache y es reciente (< 60s)
            var cached = LoadCache();
            if (cached != null && (DateTime.UtcNow - cached.LastChecked).TotalMinutes < 5)
            {
                Console.WriteLine("[DEBUG] Usando cache (última comprobación hace menos de 5 minutos).");
                return cached;
            }

            try
            {
                // 2. Versión local (3 dígitos)
                var exePath = Assembly.GetEntryAssembly()?.Location ?? "";
                var fvi = FileVersionInfo.GetVersionInfo(exePath);
                var productVersionClean = fvi.ProductVersion?.Split('+')[0] ?? "0.0.0";
                var currentVersion = new Version(productVersionClean);

                // 3. Llamada a GitHub API
                httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("VManager-Updater");
                var url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
                var json = await httpClient.GetStringAsync(url);

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                var releaseNotes = root.GetProperty("body").GetString() ?? "";

                // Extraer versión remota limpia
                var tagName = root.GetProperty("tag_name").GetString() ?? "0.0.0";
                if (tagName.StartsWith("v"))
                    tagName = tagName[1..];

                var numericPart = tagName.Split(new[] { '+', '-' }, StringSplitOptions.RemoveEmptyEntries)[0];
                var segments = numericPart.Split('.');

                var major = segments.Length > 0 ? int.Parse(segments[0]) : 0;
                var minor = segments.Length > 1 ? int.Parse(segments[1]) : 0;
                var build = segments.Length > 2 ? int.Parse(segments[2]) : 0;

                var latestVersion = new Version(major, minor, build);
                Console.WriteLine("latestVersion: " + latestVersion);

                // 4. Buscar asset según plataforma
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
            catch (HttpRequestException ex) when (ex.Message.Contains("rate limit"))
            {
                Console.WriteLine("[DEBUG] Límite de GitHub excedido, devolviendo cache si existe...");
                return cached;
            }
            catch (HttpRequestException ex)
            {
                Console.WriteLine($"[DEBUG] Error al revisar actualizaciones: {ex.Message}");
                return cached;
            }
        }

        private static string GetPlatformAssetName()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "win-x64";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return "linux-x64";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return "osx-x64";
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
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Error guardando cache: {ex.Message}");
            }
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
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG] Error leyendo cache: {ex.Message}");
            }
            return null;
        }
    }
}
