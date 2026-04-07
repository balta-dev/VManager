using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using VManager.Services.Models;

namespace VManager.Services
{
    public class HardwareAccelerationCacheService
    {
        private readonly IHardwareAccelerationService _hardwareAccelerationService;
        private readonly string _cacheFile;
        private CodecCache? _cache;

        public HardwareAccelerationCacheService(IHardwareAccelerationService hardwareAccelerationService)
        {
            _hardwareAccelerationService = hardwareAccelerationService;
            string cacheFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VManager", "cache");

            Directory.CreateDirectory(cacheFolder);

            _cacheFile = Path.Combine(cacheFolder, "codecs.json");
        }

        public async Task<CodecCache> LoadOrBuildCacheAsync()
        {
            if (_cache != null) return _cache;

            if (File.Exists(_cacheFile))
            {
                try
                {
                    string json = await File.ReadAllTextAsync(_cacheFile);
                    _cache = JsonSerializer.Deserialize<CodecCache>(json, VManagerJsonContext.Default.CodecCache)!;
                    if (_cache != null)
                        return _cache;
                }
                catch
                {
                    await RefreshCacheAsync();
                }
            }

            return await RefreshCacheAsync();
        }

        public async Task<CodecCache> RefreshCacheAsync()
        {
            var video = await _hardwareAccelerationService.GetAvailableVideoCodecsAsync();
            var audio = await _hardwareAccelerationService.GetAvailableAudioCodecsAsync();
            var hw    = await _hardwareAccelerationService.GetHardwareCapabilitiesAsync();

            _cache = new CodecCache
            {
                VideoCodecs = new List<string>(video),
                AudioCodecs = new List<string>(audio),
                Hardware    = hw
            };

            PrintSummary(hw, video, audio);

            Console.WriteLine("Guardando datos en caché...");
            await SaveCacheAsync(_cache);

            return _cache;
        }

        private static void PrintSummary(
            HardwareCapabilities hw,
            IReadOnlyList<string> video,
            IReadOnlyList<string> audio)
        {
            Console.WriteLine();
            Console.WriteLine("══════════════════════════════════════════");
            Console.WriteLine("          RESUMEN DE HARDWARE             ");
            Console.WriteLine("══════════════════════════════════════════");

            // Sistema operativo
            Console.Write("  OS:  ");
            if (hw.Windows) Console.WriteLine("Windows");
            else if (hw.Linux) Console.WriteLine("Linux");
            else if (hw.Mac)   Console.WriteLine("macOS");
            else               Console.WriteLine("Desconocido");

            // GPU vendor(s)
            var gpus = new List<string>();
            if (hw.Nvidia) gpus.Add("NVIDIA");
            if (hw.AMD)    gpus.Add("AMD");
            if (hw.Intel)  gpus.Add("Intel");
            Console.WriteLine($"  GPU: {(gpus.Count > 0 ? string.Join(" + ", gpus) : "No detectada")}");

            // Capacidades adicionales
            var extras = new List<string>();
            if (hw.VAAPI)                 extras.Add("VAAPI");
            if (hw.VideoToolbox)          extras.Add("VideoToolbox");
            if (hw.WindowsMediaFoundation) extras.Add("Media Foundation");
            if (extras.Count > 0)
                Console.WriteLine($"  HW extras: {string.Join(", ", extras)}");

            Console.WriteLine();

            // Codecs de video disponibles (en orden de prioridad)
            Console.WriteLine($"  Códecs de video disponibles ({video.Count}):");
            foreach (var codec in video)
                Console.WriteLine($"    • {codec}");

            Console.WriteLine();

            // Codecs de audio disponibles
            Console.WriteLine($"  Códecs de audio disponibles ({audio.Count}):");
            foreach (var codec in audio)
                Console.WriteLine($"    • {codec}");

            Console.WriteLine("══════════════════════════════════════════");
            Console.WriteLine();
        }

        private async Task SaveCacheAsync(CodecCache cache)
        {
            try
            {
                string json = JsonSerializer.Serialize(cache, VManagerJsonContext.Default.CodecCache);
                await File.WriteAllTextAsync(_cacheFile, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error al escribir el archivo de caché: {ex.Message}");
            }
        }
    }
    
}
