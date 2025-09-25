using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Threading;

namespace VManager.Services
{
    public class CodecService : ICodecService
    {
        private readonly string _ffmpegPath;
        private readonly SemaphoreSlim _codecCacheLock = new SemaphoreSlim(1, 1);
        private List<string> _cachedCodecs;

        public CodecService()
        {
            _ffmpegPath = FFmpegManager.FfmpegPath;
        }

        public async Task<IReadOnlyList<string>> GetAvailableVideoCodecsAsync()
        {
            var allCodecs = await GetCodecsAsync();
            var hardware = await DetectHardwareAsync();

            var candidateCodecs = new List<string>
            {
                "libx264", "libx265", "libvpx-vp9", "libx264rgb"
            };

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                AddWindowsVideoCodecs(candidateCodecs, allCodecs, hardware);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                AddLinuxVideoCodecs(candidateCodecs, allCodecs, hardware);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                AddMacVideoCodecs(candidateCodecs, allCodecs, hardware);
            }

            var workingCodecs = await TestVideoCodecsBatchAsync(candidateCodecs.Intersect(allCodecs).Distinct().ToList());

            var priority = GetVideoCodecPriority();
            return workingCodecs
                .OrderByDescending(c => priority.GetValueOrDefault(c, 0))
                .ThenBy(c => c)
                .ToList();
        }

        private void AddWindowsVideoCodecs(List<string> codecs, List<string> allCodecs, HardwareCapabilities hardware)
        {
            if (hardware.Nvidia) codecs.AddRange(allCodecs.Where(c => c.Contains("nvenc")));
            if (hardware.AMD) codecs.AddRange(allCodecs.Where(c => c.Contains("amf")));
            if (hardware.Intel) codecs.AddRange(allCodecs.Where(c => c.Contains("qsv")));
            if (hardware.WindowsMediaFoundation) codecs.AddRange(allCodecs.Where(c => c.Contains("_mf")));
        }

        private void AddLinuxVideoCodecs(List<string> codecs, List<string> allCodecs, HardwareCapabilities hardware)
        {
            if (hardware.VAAPI) codecs.AddRange(allCodecs.Where(c => c.Contains("vaapi")));
            if (hardware.Nvidia) codecs.AddRange(allCodecs.Where(c => c.Contains("nvenc")));
            codecs.AddRange(allCodecs.Where(c => c.Contains("v4l2m2m") || c.Contains("vulkan")));
        }

        private void AddMacVideoCodecs(List<string> codecs, List<string> allCodecs, HardwareCapabilities hardware)
        {
            if (hardware.VideoToolbox) codecs.AddRange(allCodecs.Where(c => c.Contains("videotoolbox")));
        }

        private Dictionary<string, int> GetVideoCodecPriority()
        {
            return new Dictionary<string, int>
            {
                { "h264_nvenc", 100 }, { "hevc_nvenc", 95 }, { "av1_nvenc", 90 },
                { "h264_amf", 85 }, { "hevc_amf", 80 }, { "av1_amf", 75 },
                { "h264_qsv", 70 }, { "hevc_qsv", 65 }, { "av1_qsv", 60 },
                { "h264_vaapi", 55 }, { "hevc_vaapi", 50 }, { "av1_vaapi", 45 },
                { "h264_videotoolbox", 40 }, { "hevc_videotoolbox", 35 },
                { "h264_mf", 30 }, { "hevc_mf", 25 },
                { "libx264", 20 }, { "libx265", 15 }, { "libvpx-vp9", 10 },
                { "libx264rgb", 5 }
            };
        }

        public async Task<IReadOnlyList<string>> GetAvailableAudioCodecsAsync()
        {
            var allCodecs = await GetCodecsAsync();
            var candidateCodecs = allCodecs
                .Where(c => c.Contains("aac") || c.Contains("mp3") || c.Contains("opus") ||
                           c.Contains("flac") || c.Contains("vorbis"))
                .ToList();

            var workingCodecs = await TestAudioCodecsBatchAsync(candidateCodecs);

            return workingCodecs
                .OrderByDescending(c => c == "aac" ? 100 : (c == "opus" ? 50 : 0))
                .ThenBy(c => c)
                .ToList();
        }

        private async Task<List<string>> GetCodecsAsync()
        {
            await _codecCacheLock.WaitAsync();
            try
            {
                if (_cachedCodecs != null)
                {
                    return _cachedCodecs;
                }
            }
            finally
            {
                _codecCacheLock.Release();
            }

            var result = new List<string>();
            var psi = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = "-encoders",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                //Console.WriteLine($"[DEBUG]: PATH de FFMPEG: {_ffmpegPath}");
                
                using var process = new Process { StartInfo = psi };
                process.Start();
                string output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode == 0)
                {
                    var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines.SkipWhile(l => !l.Contains("Encoders:")).Skip(1))
                    {
                        var trimmed = line.Trim();
                        if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("------")) continue;

                        var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 1 && (parts[0].Contains("V") || parts[0].Contains("A")))
                        {
                            result.Add(parts[1]);
                        }
                    }
                }

                await _codecCacheLock.WaitAsync();
                try
                {
                    _cachedCodecs = result;
                }
                finally
                {
                    _codecCacheLock.Release();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching codecs: {ex.Message}");
            }

            return result;
        }

        private async Task<HardwareCapabilities> DetectHardwareAsync()
        {
            return RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? await DetectWindowsHardwareAsync()
                : RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                    ? await DetectLinuxHardwareAsync()
                    : await DetectMacHardwareAsync();
        }

        private async Task<HardwareCapabilities> DetectWindowsHardwareAsync()
        {
            var capabilities = new HardwareCapabilities();
            capabilities.Windows = true;
            
            // Detectar NVIDIA de forma independiente
            try
            {
                capabilities.Nvidia = await DetectNvidiaWindowsAsync();
                Console.WriteLine($"Resultado de DetectNvidiaWindowsAsync: Nvidia = {capabilities.Nvidia}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al detectar NVIDIA: {ex.Message}");
                capabilities.Nvidia = false;
            }

            // Detectar AMD e Intel usando wmic
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = "wmic",
                    Arguments = "path win32_VideoController get name",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                
                using var process = Process.Start(psi);
                if (process != null)
                {
                    Console.WriteLine("Proceso wmic iniciado correctamente");
                    var output = await process.StandardOutput.ReadToEndAsync();
                    var error = await process.StandardError.ReadToEndAsync();
                    await process.WaitForExitAsync();

                    var lowerOutput = output.ToLower();
                    capabilities.AMD = lowerOutput.Contains("amd") || lowerOutput.Contains("radeon");
                    capabilities.Intel = lowerOutput.Contains("intel");
                    // Opcional: respaldo para NVIDIA usando wmic
                    capabilities.Nvidia |= lowerOutput.Contains("nvidia"); // Combina con el resultado de DetectNvidiaWindowsAsync
                    capabilities.WindowsMediaFoundation = Environment.OSVersion.Version.Major >= 10;
                }
                else
                {
                    Console.WriteLine("Error: No se pudo iniciar el proceso wmic");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Excepción en wmic: {ex.Message}");
            }

            Console.WriteLine($"Resultado final: Nvidia = {capabilities.Nvidia}, AMD = {capabilities.AMD}, Intel = {capabilities.Intel}");
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
            capabilities.Linux = true;
            try
            {
                if (Directory.Exists("/sys/class/drm"))
                {
                    foreach (var device in Directory.GetDirectories("/sys/class/drm"))
                    {
                        var vendorFile = Path.Combine(device, "device", "vendor");
                        if (File.Exists(vendorFile))
                        {
                            var vendor = await File.ReadAllTextAsync(vendorFile);
                            vendor = vendor.Trim();
                            if (vendor == "0x1002") capabilities.AMD = true;
                            if (vendor == "0x8086") capabilities.Intel = true;
                        }
                    }
                }
                capabilities.VAAPI = await DetectVaapiAsync();
                capabilities.Nvidia = await DetectNvidiaLinuxAsync();
            }
            catch { }

            return capabilities;
        }

        private async Task<bool> DetectVaapiAsync()
        {
            if (!File.Exists("/dev/dri/renderD128")) return false;

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
                return true;
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

                using var process = Process.Start(psi);
                if (process == null) return false;

                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync();
                return !string.IsNullOrWhiteSpace(output) && output.Contains("GPU");
            }
            catch
            {
                return File.Exists("/proc/driver/nvidia/version");
            }
        }

        private async Task<HardwareCapabilities> DetectMacHardwareAsync()
        {
            return new HardwareCapabilities 
            { 
                VideoToolbox = true,
                Mac = true
            };
        }

        private string GetTestCommandForCodec(string codecName)
        {
            var lowerCodec = codecName.ToLower();
            return lowerCodec switch
            {
                var name when (name.Contains("vaapi") && !RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) ||
                              (name.Contains("videotoolbox") && !RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) ||
                              ((name.Contains("mf") || name.Contains("amf")) && !RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) => null,

                // VAAPI Linux
                var name when name.Contains("vaapi") =>
                    $"-init_hw_device vaapi=va:/dev/dri/renderD128 -f lavfi -i testsrc2=duration=0.001:size=130x130:rate=1 -vf format=nv12,hwupload=extra_hw_frames=4 -c:v {codecName} -b:v 1k -f null -",

                // NVENC Windows/Linux
                var name when name.Contains("nvenc") =>
                    $"-f lavfi -i testsrc2=duration=0.001:size=130x130:rate=1 -c:v {codecName} -preset ultrafast -b:v 1k -f null -",

                // QSV Intel
                var name when name.Contains("qsv") =>
                    RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                        ? $"-init_hw_device qsv=hw -f lavfi -i testsrc2=duration=0.001:size=130x130:rate=1 -vf hwupload=extra_hw_frames=4 -c:v {codecName} -preset ultrafast -b:v 1k -f null -"
                        : $"-init_hw_device vaapi=va:/dev/dri/renderD128 -init_hw_device qsv=hw@va -f lavfi -i testsrc2=duration=0.001:size=130x130:rate=1 -vf format=nv12,hwupload=extra_hw_frames=4 -c:v {codecName} -preset ultrafast -b:v 1k -f null -",

                // VideoToolbox macOS
                var name when name.Contains("videotoolbox") =>
                    $"-f lavfi -i testsrc2=duration=0.001:size=130x130:rate=1 -c:v {codecName} -b:v 1k -f null -",

                // Media Foundation Windows
                var name when name.Contains("mf") =>
                    $"-f lavfi -i testsrc2=duration=0.001:size=130x130:rate=1 -c:v {codecName} -b:v 1k -f null -",

                // AMD AMF Windows
                var name when name.Contains("amf") =>
                    $"-init_hw_device d3d11va=dx11 -f lavfi -i testsrc2=duration=0.001:size=130x130:rate=1 -vf hwupload=extra_hw_frames=4 -c:v {codecName} -b:v 1k -f null -",

                // Otros codecs software
                _ => $"-f lavfi -i testsrc2=duration=0.001:size=130x130:rate=1 -c:v {codecName} -b:v 1k -f null -"
            };
        }
        
        private async Task<List<string>> TestVideoCodecsBatchAsync(List<string> codecs)
        {
            var workingCodecs = new List<string>();
            var semaphore = new SemaphoreSlim(Environment.ProcessorCount, Environment.ProcessorCount);

            var tasks = codecs.Select(async codec =>
            {
                await semaphore.WaitAsync();
                try
                {
                    string testCommand = GetTestCommandForCodec(codec);
                    if (testCommand == null) return null;

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
                    if (process == null) return null;

                    await process.WaitForExitAsync();
                    return process.ExitCode == 0 ? codec : null;
                }
                catch
                {
                    return null;
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToArray();

            var results = await Task.WhenAll(tasks);
            workingCodecs.AddRange(results.Where(r => r != null));

            return workingCodecs;
        }

        private async Task<List<string>> TestAudioCodecsBatchAsync(List<string> codecs)
        {
            var workingCodecs = new List<string>();
            var semaphore = new SemaphoreSlim(Environment.ProcessorCount, Environment.ProcessorCount);

            var tasks = codecs.Select(async codec =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = _ffmpegPath,
                        Arguments = $"-f lavfi -i sine=frequency=440:duration=0.1 -c:a {codec} -f null -",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(psi);
                    if (process == null) return null;

                    await process.WaitForExitAsync();
                    return process.ExitCode == 0 ? codec : null;
                }
                catch
                {
                    return null;
                }
                finally
                {
                    semaphore.Release();
                }
            }).ToArray();

            var results = await Task.WhenAll(tasks);
            workingCodecs.AddRange(results.Where(r => r != null));

            return workingCodecs;
        }

        public async Task<HardwareCapabilities> GetHardwareCapabilitiesAsync()
        {
            return await DetectHardwareAsync();
        }

        public void Dispose()
        {
            _codecCacheLock?.Dispose();
        }
    }

    public class HardwareCapabilities
    {
        
        public bool Windows { get; set; }
        
        public bool Linux { get; set; }
        
        public bool Mac { get; set; }
        public bool Nvidia { get; set; }
        public bool AMD { get; set; }
        public bool Intel { get; set; }
        public bool VAAPI { get; set; }
        public bool WindowsMediaFoundation { get; set; }
        public bool VideoToolbox { get; set; }

        public override string ToString()
        {
            var capabilities = new List<string>();
            
            if (Windows) capabilities.Add("Windows");
            if (Linux) capabilities.Add("Linux");
            if (Mac) capabilities.Add("Mac");
            
            if (Nvidia) capabilities.Add("NVIDIA");
            if (AMD) capabilities.Add("AMD");
            if (Intel) capabilities.Add("Intel");
            if (VAAPI) capabilities.Add("VAAPI");
            if (WindowsMediaFoundation) capabilities.Add("Windows Media Foundation");
            if (VideoToolbox) capabilities.Add("VideoToolbox");
            
            return capabilities.Any() ? string.Join(", ", capabilities) : "Software Only";
        }
    }
}