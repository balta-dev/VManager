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
            var hw = await _hardwareAccelerationService.GetHardwareCapabilitiesAsync();

            _cache = new CodecCache
            {
                VideoCodecs = new List<string>(video),
                AudioCodecs = new List<string>(audio),
                Hardware = hw
            };

            Console.WriteLine("[DEBUG]: ¡Caché regenerado!");
            
            await SaveCacheAsync(_cache);
            return _cache;
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
                Console.WriteLine($"[ERROR] Error writing codec cache file: {ex.Message}");
            }
        }
    }
    
}
