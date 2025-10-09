using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace VManager.Services
{
    public class CodecCacheService
    {
        private readonly ICodecService _codecService;
        private readonly string _cacheFile;
        private CodecCache? _cache;

        public CodecCacheService(ICodecService codecService)
        {
            _codecService = codecService;
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
                    _cache = JsonSerializer.Deserialize<CodecCache>(json);
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
            var video = await _codecService.GetAvailableVideoCodecsAsync();
            var audio = await _codecService.GetAvailableAudioCodecsAsync();
            var hw = await _codecService.GetHardwareCapabilitiesAsync();

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
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(cache, options);
                await File.WriteAllTextAsync(_cacheFile, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error writing codec cache file: {ex.Message}");
            }
        }
    }

    public class CodecCache
    {
        public List<string> VideoCodecs { get; set; } = new();
        public List<string> AudioCodecs { get; set; } = new();
        public HardwareCapabilities Hardware { get; set; } = new();
    }
}
