using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace VManager.Services
{
    public class CodecService : ICodecService
    {
        private readonly string _ffmpegPath;

        public CodecService()
        {
            _ffmpegPath = "/usr/bin/ffmpeg"; //POR FAVOR CAMBIA ESTO
            if (OperatingSystem.IsWindows())
                _ffmpegPath += ".exe";
        }

        public async Task<IReadOnlyList<string>> GetAvailableVideoCodecsAsync()
        {
            var codecs = await GetCodecsAsync();
            var hardware = await DetectHardwareAsync();

            // Filtrado según disponibilidad de GPU
            if (!hardware.Nvidia)
                codecs.RemoveAll(c => c.Contains("nvenc"));

            if (!hardware.AMD)
                codecs.RemoveAll(c => c.Contains("amf"));

            if (!hardware.Intel)
                codecs.RemoveAll(c => c.Contains("qsv"));

            if (!hardware.VAAPI)
                codecs.RemoveAll(c => c.Contains("vaapi"));

            var priority = new Dictionary<string, int>
            {
                { "h264_nvenc", 100 }, { "hevc_nvenc", 90 },
                { "h264_qsv", 80 }, { "hevc_qsv", 70 },
                { "h264_amf", 60 }, { "hevc_amf", 50 },
                { "h264_vaapi", 40 }, { "hevc_vaapi", 30 },
                { "libx264", 20 }, { "libx265", 10 }
            };
            
            var sorted = codecs
                .Where(c => c.Contains("264") || c.Contains("265") || c == "copy"
                            || c.Contains("vp8") || c.Contains("vp9"))
                .OrderByDescending(c => priority.ContainsKey(c) ? priority[c] : 0)
                .ThenBy(c => c)
                .ToList();
            
            return sorted;
        }

        public async Task<IReadOnlyList<string>> GetAvailableAudioCodecsAsync()
        {
            var codecs = await GetCodecsAsync();
            return codecs
                .Where(c => c.Contains("aac") || c.Contains("mp3")
                         || c == "copy" || c.Contains("opus"))
                .ToList();
        }

        private async Task<List<string>> GetCodecsAsync()
        {
            var psi = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = "-encoders",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var result = new List<string>();

            using var process = new Process { StartInfo = psi };
            process.Start();
            string output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                var lines = output.Split('\n');

                foreach (var line in lines)
                {
                    var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 1 && !parts[1].StartsWith("Codecs"))
                    {
                        result.Add(parts[1]);
                    }
                }
            }

            return result;
        }
        
        private async Task<(bool Nvidia, bool AMD, bool Intel, bool VAAPI)> DetectHardwareAsync()
        {
            bool nvidia = false, amd = false, intel = false, vaapi = false;

            // Detectar VAAPI
            vaapi = File.Exists("/dev/dri/renderD128");

            // Detectar NVIDIA con nvidia-smi
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "nvidia-smi",
                    Arguments = "-L",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var p = Process.Start(psi);
                string output = await p.StandardOutput.ReadToEndAsync();
                await p.WaitForExitAsync();
                nvidia = !string.IsNullOrWhiteSpace(output);
            }
            catch { }

            // Para Intel y AMD en Linux, VAAPI suele cubrir ambos, así que podemos simplificar:
            // - AMD: buscar codecs *_amf en la lista de encoders después de GetCodecsAsync
            // - Intel: buscar codecs *_qsv después de GetCodecsAsync

            return (nvidia, amd, intel, vaapi);
        }
        
    }
}
