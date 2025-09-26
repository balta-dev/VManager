using System;
using System.Diagnostics;
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

        public class UpdateInfo
        {
            public Version CurrentVersion { get; set; }
            public Version LatestVersion { get; set; }
            public string DownloadUrl { get; set; }
            public string ReleaseNotes { get; set; }
            public bool UpdateAvailable => LatestVersion > CurrentVersion;
        }

        public static async Task<UpdateInfo> CheckForUpdateAsync()
        {
            // 1. Versión local (3 dígitos)
            var exePath = Assembly.GetEntryAssembly()?.Location ?? "";
            var fvi = System.Diagnostics.FileVersionInfo.GetVersionInfo(exePath);
            var productVersionClean = fvi.ProductVersion?.Split('+')[0] ?? "0.0.0";
            var currentVersion = new Version(productVersionClean);
            

            // 2. Llamada a GitHub API
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("VManager-Updater");
            var url = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
            var json = await httpClient.GetStringAsync(url);
            
            Console.WriteLine("Buscando actualizaciones...");

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            var releaseNotes = root.GetProperty("body").GetString() ?? "";

            // 3. Extraer versión remota limpia
            var tagName = root.GetProperty("tag_name").GetString() ?? "0.0.0";

            // DEBUG: imprimir lo que viene del API
            //Console.WriteLine("[DEBUG]: tagName raw: " + tagName);

            // Quitar 'v'
            if (tagName.StartsWith("v"))
                tagName = tagName[1..];
    
            //Console.WriteLine("Versión encontrada: " + tagName);

            // Intentar separar la parte numérica
            var numericPart = tagName.Split(new[] { '+', '-' }, StringSplitOptions.RemoveEmptyEntries)[0];
            //Console.WriteLine("numericPart: " + numericPart);

            // Separar por puntos
            var segments = numericPart.Split('.');
            //Console.WriteLine("segments: " + string.Join(", ", segments));

            // Finalmente crear Version
            int major = segments.Length > 0 ? int.Parse(segments[0]) : 0;
            int minor = segments.Length > 1 ? int.Parse(segments[1]) : 0;
            int build = segments.Length > 2 ? int.Parse(segments[2]) : 0;

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

            return new UpdateInfo
            {
                CurrentVersion = currentVersion,
                LatestVersion = latestVersion,
                DownloadUrl = downloadUrl,
                ReleaseNotes = releaseNotes
            };
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
    }
}
