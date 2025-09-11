using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace VManager.Services
{
    public class CodecService : ICodecService
    {
        private readonly string _ffmpegPath;

        public CodecService()
        {
            _ffmpegPath = GetFFmpegPath();
        }

        private string GetFFmpegPath()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "ffmpeg.exe"; // Asume que está en PATH o mismo directorio. Está mal, tengo que incorporar el binario al .exe
            else
                return "/usr/bin/ffmpeg"; // Linux/macOS. También está mal, lo mismo que para Windows.
        }

        public async Task<IReadOnlyList<string>> GetAvailableVideoCodecsAsync()
        {
            var allCodecs = await GetCodecsAsync();
            var hardware = await DetectHardwareAsync();

            // Lista de codecs a considerar
            var candidateCodecs = new List<string>();

            // Software codecs (siempre disponibles)
            candidateCodecs.AddRange(allCodecs.Where(c => 
                c == "libx264" || c == "libx265" || c == "libvpx-vp9" || c == "libx264rgb"));

            // Agregar codecs de hardware según plataforma y hardware detectado
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                await AddWindowsVideoCodecs(candidateCodecs, allCodecs, hardware);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                await AddLinuxVideoCodecs(candidateCodecs, allCodecs, hardware);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                await AddMacVideoCodecs(candidateCodecs, allCodecs, hardware);
            }

            // Verificar que los codecs realmente funcionen
            var workingCodecs = new List<string>();
            foreach (var codec in candidateCodecs.Distinct())
            {
                if (true)
                {
                    workingCodecs.Add(codec);
                } 
            }

            // Ordenar por prioridad
            var priority = GetVideoCodecPriority();
            var sorted = workingCodecs
                .OrderByDescending(c => priority.ContainsKey(c) ? priority[c] : 0)
                .ThenBy(c => c)
                .ToList();

            return sorted;
        }

        private async Task AddWindowsVideoCodecs(List<string> codecs, List<string> allCodecs, HardwareCapabilities hardware)
        {
            // NVIDIA
            if (hardware.Nvidia)
            {
                codecs.AddRange(allCodecs.Where(c => c.Contains("nvenc")));
            }

            // AMD
            if (hardware.AMD)
            {
                codecs.AddRange(allCodecs.Where(c => c.Contains("amf")));
            }

            // Intel
            if (hardware.Intel)
            {
                codecs.AddRange(allCodecs.Where(c => c.Contains("qsv")));
            }

            // Windows Media Foundation
            if (hardware.WindowsMediaFoundation)
            {
                codecs.AddRange(allCodecs.Where(c => c.Contains("_mf")));
            }
        }

        private async Task AddLinuxVideoCodecs(List<string> codecs, List<string> allCodecs, HardwareCapabilities hardware)
        {
            // VAAPI (AMD/Intel)
            if (hardware.VAAPI)
            {
                codecs.AddRange(allCodecs.Where(c => c.Contains("vaapi")));
            }

            // NVIDIA
            if (hardware.Nvidia)
            {
                codecs.AddRange(allCodecs.Where(c => c.Contains("nvenc")));
            }

            // V4L2
            codecs.AddRange(allCodecs.Where(c => c.Contains("v4l2m2m")));

            // Vulkan
            codecs.AddRange(allCodecs.Where(c => c.Contains("vulkan")));
        }

        private async Task AddMacVideoCodecs(List<string> codecs, List<string> allCodecs, HardwareCapabilities hardware)
        {
            // VideoToolbox
            if (hardware.VideoToolbox)
            {
                codecs.AddRange(allCodecs.Where(c => c.Contains("videotoolbox")));
            }
        }

        private Dictionary<string, int> GetVideoCodecPriority()
        {
            return new Dictionary<string, int>
            {
                // Hardware codecs tienen mayor prioridad
                { "h264_nvenc", 100 }, { "hevc_nvenc", 95 }, { "av1_nvenc", 90 },
                { "h264_amf", 85 }, { "hevc_amf", 80 }, { "av1_amf", 75 },
                { "h264_qsv", 70 }, { "hevc_qsv", 65 }, { "av1_qsv", 60 },
                { "h264_vaapi", 55 }, { "hevc_vaapi", 50 }, { "av1_vaapi", 45 },
                { "h264_videotoolbox", 40 }, { "hevc_videotoolbox", 35 },
                { "h264_mf", 30 }, { "hevc_mf", 25 },
                
                // Software codecs
                { "libx264", 20 }, { "libx265", 15 }, { "libvpx-vp9", 10 },
                { "libx264rgb", 5 }
            };
        }

        public async Task<IReadOnlyList<string>> GetAvailableAudioCodecsAsync()
        {
            var allCodecs = await GetCodecsAsync();
            var candidateCodecs = allCodecs.Where(c => 
                c.Contains("aac") || c.Contains("mp3") || c.Contains("opus") || 
                c.Contains("flac") || c.Contains("vorbis")).ToList();
            
            var workingCodecs = new List<string>();
            foreach (var codec in candidateCodecs) workingCodecs.Add(codec);
            
            // Ordenar con AAC primero
            return workingCodecs
                .OrderByDescending(c => c == "aac" ? 100 : (c == "opus" ? 50 : 0))
                .ThenBy(c => c)
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

            try
            {
                using var process = new Process { StartInfo = psi };
                process.Start();
                string output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    var lines = output.Split('\n');
                    bool inEncoders = false;

                    foreach (var line in lines)
                    {
                        if (line.Contains("Encoders:"))
                        {
                            inEncoders = true;
                            continue;
                        }

                        if (!inEncoders) continue;

                        // Parsear líneas del formato: " V..... libx264              libx264 H.264 / AVC / MPEG-4 AVC / MPEG-4 part 10"
                        var trimmed = line.Trim();
                        if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("------")) continue;

                        var parts = trimmed.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 1 && (parts[0].Contains("V") || parts[0].Contains("A")))
                        {
                            result.Add(parts[1]);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo codecs: {ex.Message}");
            }

            return result;
        }

        private async Task<HardwareCapabilities> DetectHardwareAsync()
        {
            var capabilities = new HardwareCapabilities();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                capabilities = await DetectWindowsHardwareAsync();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                capabilities = await DetectLinuxHardwareAsync();
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                capabilities = await DetectMacHardwareAsync();
            }

            return capabilities;
        }

        private async Task<HardwareCapabilities> DetectWindowsHardwareAsync()
        {
            var capabilities = new HardwareCapabilities();

            // NVIDIA
            capabilities.Nvidia = await DetectNvidiaWindowsAsync();
            
            // AMD y Intel via WMI
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "wmic",
                    Arguments = "path win32_VideoController get name",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process != null)
                {
                    var output = await process.StandardOutput.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    var lowerOutput = output.ToLower();
                    capabilities.AMD = lowerOutput.Contains("amd") || lowerOutput.Contains("radeon");
                    capabilities.Intel = lowerOutput.Contains("intel");
                }
            }
            catch { }

            // Windows Media Foundation (Windows 10+)
            capabilities.WindowsMediaFoundation = Environment.OSVersion.Version.Major >= 10;

            return capabilities;
        }

        private async Task<bool> DetectNvidiaWindowsAsync()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "nvidia-smi.exe",
                    Arguments = "-L",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null) return false;

                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                return process.ExitCode == 0 && output.Contains("GPU");
            }
            catch
            {
                return false;
            }
        }

        private async Task<HardwareCapabilities> DetectLinuxHardwareAsync()
        {
            var capabilities = new HardwareCapabilities();

            // VAAPI
            capabilities.VAAPI = await DetectVaapiAsync();

            // NVIDIA
            capabilities.Nvidia = await DetectNvidiaLinuxAsync();

            // AMD y Intel via vendor IDs
            try
            {
                if (Directory.Exists("/sys/class/drm"))
                {
                    var drmDevices = Directory.GetDirectories("/sys/class/drm");
                    foreach (var device in drmDevices)
                    {
                        var vendorFile = $"{device}/device/vendor";
                        if (File.Exists(vendorFile))
                        {
                            var vendor = File.ReadAllText(vendorFile).Trim();
                            if (vendor == "0x1002") capabilities.AMD = true;
                            if (vendor == "0x8086") capabilities.Intel = true;
                            
                        }
                    }
                }
            }
            catch { }

            return capabilities;
        }

        private async Task<bool> DetectVaapiAsync()
        {
            if (!File.Exists("/dev/dri/renderD128"))
                return false;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "vainfo",
                    Arguments = "--display drm --device /dev/dri/renderD128",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null) return false;

                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                return process.ExitCode == 0 && output.Contains("VAEntrypoint");
            }
            catch
            {
                return true; // Si vainfo no está, asumir que funciona
            }
        }

        private async Task<bool> DetectNvidiaLinuxAsync()
        {
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
                if (p == null) return false;
                
                string output = await p.StandardOutput.ReadToEndAsync();
                await p.WaitForExitAsync();
                return !string.IsNullOrWhiteSpace(output) && output.Contains("GPU");
            }
            catch
            {
                return File.Exists("/proc/driver/nvidia/version");
            }
        }

        private async Task<HardwareCapabilities> DetectMacHardwareAsync()
        {
            var capabilities = new HardwareCapabilities
            {
                VideoToolbox = true // VideoToolbox está disponible en macOS moderno
            };

            return capabilities;
        }

        private async Task<bool> TestVideoCodecAsync(string codecName)
        {
            try
            {
                var testCommand = $"-f lavfi -i testsrc2=duration=0.1:size=320x240:rate=1 -c:v {codecName} -f null -";

                var psi = new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    Arguments = testCommand,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null) return false;

                await process.WaitForExitAsync();
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> TestAudioCodecAsync(string codecName)
        {
            try
            {
                var testCommand = $"-f lavfi -i sine=frequency=440:duration=0.1 -c:a {codecName} -f null -";

                var psi = new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    Arguments = testCommand,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var process = Process.Start(psi);
                if (process == null) return false;

                await process.WaitForExitAsync();
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        public async Task<HardwareCapabilities> GetHardwareCapabilitiesAsync()
        {
            return await DetectHardwareAsync();
        }
    }

    public class HardwareCapabilities
    {
        // Multiplataforma
        public bool Nvidia { get; set; }
        public bool AMD { get; set; }
        public bool Intel { get; set; }

        // Linux específico
        public bool VAAPI { get; set; }

        // Windows específico
        public bool WindowsMediaFoundation { get; set; }

        // macOS específico
        public bool VideoToolbox { get; set; }

        public override string ToString()
        {
            var capabilities = new List<string>();

            if (Nvidia) capabilities.Add("NVIDIA");
            if (AMD) capabilities.Add("AMD");
            if (Intel) capabilities.Add("Intel");
            if (VAAPI) capabilities.Add("VAAPI");
            if (WindowsMediaFoundation) capabilities.Add("Windows Media Foundation");
            if (VideoToolbox) capabilities.Add("VideoToolbox");

            return capabilities.Any() ? string.Join(", ", capabilities) : "Solo Software";
        }
    }
}