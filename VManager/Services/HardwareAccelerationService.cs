using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using VManager.Services.Models;

namespace VManager.Services
{
    public class HardwareAccelerationService : IHardwareAccelerationService
    {
        private readonly string _ffmpegPath;

        // Lazy<Task> garantiza ejecución única sin race condition.
        private readonly Lazy<Task<List<string>>> _codecsLazy;
        private readonly Lazy<Task<HardwareCapabilities>> _hardwareLazy;

        private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(15);

        public HardwareAccelerationService()
        {
            _ffmpegPath   = FFmpegManager.FfmpegPath;
            _codecsLazy   = new Lazy<Task<List<string>>>(FetchCodecsFromProcessAsync);
            _hardwareLazy = new Lazy<Task<HardwareCapabilities>>(DetectHardwareCoreAsync);
        }

        // ─────────────────────────────────────────────────────────────
        //  CODECS PÚBLICOS
        // ─────────────────────────────────────────────────────────────

        public async Task<IReadOnlyList<string>> GetAvailableVideoCodecsAsync()
        {
            var allCodecs = await GetCodecsAsync();
            var hardware  = await DetectHardwareAsync();

            var candidateCodecs = new List<string>
            {
                "libx264", "libx265", "libvpx-vp9", "libx264rgb",
                "libaom-av1", "libsvtav1", "librav1e"
            };

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                AddWindowsVideoCodecs(candidateCodecs, allCodecs, hardware);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                AddLinuxVideoCodecs(candidateCodecs, allCodecs, hardware);
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                AddMacVideoCodecs(candidateCodecs, allCodecs, hardware);

            var toTest       = candidateCodecs.Intersect(allCodecs).Distinct().ToList();
            var workingCodecs = await TestCodecsBatchAsync(toTest, GetTestCommandForCodec);

            var priority = GetVideoCodecPriority();
            return workingCodecs
                .OrderByDescending(c => priority.GetValueOrDefault(c, 0))
                .ThenBy(c => c)
                .ToList();
        }

        public async Task<IReadOnlyList<string>> GetAvailableAudioCodecsAsync()
        {
            var allCodecs = await GetCodecsAsync();
            var candidates = allCodecs
                .Where(c => c.Contains("aac") || c.Contains("mp3") || c.Contains("opus") ||
                            c.Contains("flac") || c.Contains("vorbis"))
                .ToList();

            var workingCodecs = await TestCodecsBatchAsync(candidates, GetAudioTestCommand);

            var priority = GetAudioCodecPriority();
            return workingCodecs
                .OrderByDescending(c => priority.GetValueOrDefault(c, 0))
                .ThenBy(c => c)
                .ToList();
        }

        public async Task<HardwareCapabilities> GetHardwareCapabilitiesAsync()
            => await DetectHardwareAsync();

        // ─────────────────────────────────────────────────────────────
        //  CACHE DE CODECS (ffmpeg -encoders)
        // ─────────────────────────────────────────────────────────────

        private Task<List<string>> GetCodecsAsync() => _codecsLazy.Value;

        private async Task<List<string>> FetchCodecsFromProcessAsync()
        {
            var result = new List<string>();
            var psi = new ProcessStartInfo
            {
                FileName               = _ffmpegPath,
                Arguments              = "-encoders",
                RedirectStandardOutput = true,
                RedirectStandardError  = true,
                UseShellExecute        = false,
                CreateNoWindow         = true
            };

            try
            {
                using var process = new Process { StartInfo = psi };
                process.Start();

                using var cts = new CancellationTokenSource(ProcessTimeout);
                string output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync(cts.Token);

                if (process.ExitCode == 0)
                {
                    var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines.SkipWhile(l => !l.Contains("Encoders:")).Skip(1))
                    {
                        var trimmed = line.Trim();
                        if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("------")) continue;

                        var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 1 && (parts[0].Contains("V") || parts[0].Contains("A")))
                            result.Add(parts[1]);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] Error al obtener codecs de ffmpeg: {ex.Message}");
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────────
        //  DETECCIÓN DE HARDWARE
        // ─────────────────────────────────────────────────────────────

        private Task<HardwareCapabilities> DetectHardwareAsync() => _hardwareLazy.Value;

        private Task<HardwareCapabilities> DetectHardwareCoreAsync()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return DetectWindowsHardwareAsync();
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return DetectLinuxHardwareAsync();
            return DetectMacHardwareAsync();
        }

        // ── Windows ──────────────────────────────────────────────────

        private async Task<HardwareCapabilities> DetectWindowsHardwareAsync()
        {
            var cap = new HardwareCapabilities { Windows = true };

            // 1) nvidia-smi (más fiable para NVIDIA que CIM)
            cap.Nvidia = await DetectNvidiaWindowsAsync();
            Console.WriteLine($"  nvidia-smi: Nvidia = {cap.Nvidia}");

            // 2) Get-CimInstance Win32_VideoController (reemplaza wmic, sin System.Management)
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName               = "powershell.exe",
                    Arguments              = "-NoProfile -NonInteractive -Command " +
                                            "\"(Get-CimInstance Win32_VideoController).Name -join '|'\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                };

                using var process = Process.Start(psi);
                if (process != null)
                {
                    using var cts = new CancellationTokenSource(ProcessTimeout);
                    var output = await process.StandardOutput.ReadToEndAsync();
                    await process.WaitForExitAsync(cts.Token);

                    var lowerOutput = output.ToLowerInvariant();
                    Console.WriteLine($"  CimInstance GPU detectado: {output.Trim().Replace("|", ", ")}");

                    cap.AMD    = lowerOutput.Contains("amd") || lowerOutput.Contains("radeon");
                    cap.Intel  = lowerOutput.Contains("intel");
                    cap.Nvidia |= lowerOutput.Contains("nvidia"); // combina con nvidia-smi
                }
                else
                {
                    Console.WriteLine("[WARN] No se pudo iniciar powershell para detectar GPU.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] CimInstance Win32_VideoController: {ex.Message}");
            }

            cap.WindowsMediaFoundation = Environment.OSVersion.Version.Major >= 10;

            return cap;
        }

        private async Task<bool> DetectNvidiaWindowsAsync()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName               = "nvidia-smi.exe",
                    Arguments              = "-L",
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                };

                using var process = Process.Start(psi);
                if (process == null) return false;

                using var cts = new CancellationTokenSource(ProcessTimeout);
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync(cts.Token);

                return process.ExitCode == 0 && output.Contains("GPU");
            }
            catch
            {
                return false;
            }
        }

        // ── Linux ─────────────────────────────────────────────────────

        private async Task<HardwareCapabilities> DetectLinuxHardwareAsync()
        {
            var cap = new HardwareCapabilities { Linux = true };

            try
            {
                if (Directory.Exists("/sys/class/drm"))
                {
                    foreach (var device in Directory.GetDirectories("/sys/class/drm"))
                    {
                        var vendorFile = Path.Combine(device, "device", "vendor");
                        if (!File.Exists(vendorFile)) continue;

                        var vendor = (await File.ReadAllTextAsync(vendorFile)).Trim();
                        if (vendor == "0x1002") cap.AMD   = true;
                        if (vendor == "0x8086") cap.Intel = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARN] Error leyendo /sys/class/drm: {ex.Message}");
            }

            cap.VAAPI  = await DetectVaapiAsync();
            cap.Nvidia = await DetectNvidiaLinuxAsync();

            return cap;
        }

        private async Task<bool> DetectVaapiAsync()
        {
            if (!File.Exists("/dev/dri/renderD128")) return false;

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName               = "vainfo",
                    Arguments              = "--display drm --device /dev/dri/renderD128",
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                };

                using var process = Process.Start(psi);
                if (process == null) return false;  // vainfo no encontrado → no asumir VAAPI

                using var cts = new CancellationTokenSource(ProcessTimeout);
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync(cts.Token);

                return process.ExitCode == 0 && output.Contains("VAEntrypoint");
            }
            catch
            {
                // vainfo no está instalado: no podemos confirmar VAAPI
                return false;
            }
        }

        private async Task<bool> DetectNvidiaLinuxAsync()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName               = "nvidia-smi",
                    Arguments              = "-L",
                    RedirectStandardOutput = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                };

                using var process = Process.Start(psi);
                if (process == null) return File.Exists("/proc/driver/nvidia/version");

                using var cts = new CancellationTokenSource(ProcessTimeout);
                var output = await process.StandardOutput.ReadToEndAsync();
                await process.WaitForExitAsync(cts.Token);

                return !string.IsNullOrWhiteSpace(output) && output.Contains("GPU");
            }
            catch
            {
                return File.Exists("/proc/driver/nvidia/version");
            }
        }

        // ── macOS ─────────────────────────────────────────────────────

        private async Task<HardwareCapabilities> DetectMacHardwareAsync()
        {
            var cap = new HardwareCapabilities { Mac = true };

            // Verificar VideoToolbox real en lugar de asumir que siempre está disponible
            cap.VideoToolbox = await DetectVideoToolboxAsync();

            // Detectar vendor de GPU con system_profiler
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName               = "system_profiler",
                    Arguments              = "SPDisplaysDataType",
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                };

                using var process = Process.Start(psi);
                if (process != null)
                {
                    using var cts = new CancellationTokenSource(ProcessTimeout);
                    var output = await process.StandardOutput.ReadToEndAsync();
                    await process.WaitForExitAsync(cts.Token);

                    var lower = output.ToLowerInvariant();
                    cap.AMD    = lower.Contains("amd") || lower.Contains("radeon");
                    cap.Intel  = lower.Contains("intel");
                    cap.Nvidia = lower.Contains("nvidia");
                    // Apple Silicon no es ninguno de los tres, pero tiene VideoToolbox
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[WARN] system_profiler no disponible: {ex.Message}");
            }

            return cap;
        }

        private async Task<bool> DetectVideoToolboxAsync()
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName               = _ffmpegPath,
                    Arguments              = "-f lavfi -i testsrc2=duration=0.001:size=130x130:rate=1 -c:v h264_videotoolbox -b:v 1k -f null -",
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                };

                using var process = Process.Start(psi);
                if (process == null) return false;

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await process.WaitForExitAsync(cts.Token);
                return process.ExitCode == 0;
            }
            catch
            {
                return false;
            }
        }

        // ─────────────────────────────────────────────────────────────
        //  TEST DE CODECS (genérico: video y audio comparten lógica)
        // ─────────────────────────────────────────────────────────────

        private async Task<List<string>> TestCodecsBatchAsync(
            List<string> codecs,
            Func<string, string?> commandBuilder)
        {
            var semaphore = new SemaphoreSlim(
                Math.Max(1, Environment.ProcessorCount / 2),
                Math.Max(1, Environment.ProcessorCount / 2));

            var tasks = codecs.Select(async codec =>
            {
                var command = commandBuilder(codec);
                if (command == null) return null;

                await semaphore.WaitAsync();
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName               = _ffmpegPath,
                        Arguments              = command,
                        RedirectStandardOutput = true,
                        RedirectStandardError  = true,
                        UseShellExecute        = false,
                        CreateNoWindow         = true
                    };

                    using var process = Process.Start(psi);
                    if (process == null) return null;

                    using var cts = new CancellationTokenSource(ProcessTimeout);
                    await process.WaitForExitAsync(cts.Token);

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
            return results.Where(r => r != null).Cast<string>().ToList();
        }

        // ─────────────────────────────────────────────────────────────
        //  COMANDOS DE TEST
        // ─────────────────────────────────────────────────────────────

        private string? GetTestCommandForCodec(string codecName)
        {
            var name = codecName.ToLowerInvariant();

            // Descartar codecs que no corresponden al OS actual
            bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
            bool isLinux   = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
            bool isMac     = RuntimeInformation.IsOSPlatform(OSPlatform.OSX);

            if (name.Contains("vaapi")       && !isLinux)   return null;
            if (name.Contains("videotoolbox") && !isMac)    return null;
            if ((name.Contains("_mf") || name.Contains("amf")) && !isWindows) return null;

            return name switch
            {
                var n when n.Contains("vaapi") =>
                    $"-init_hw_device vaapi=va:/dev/dri/renderD128 -f lavfi -i testsrc2=duration=0.001:size=256x144:rate=1 " +
                    $"-vf format=nv12,hwupload=extra_hw_frames=4 -c:v {codecName} -b:v 1k -f null -",

                var n when n.Contains("nvenc") =>
                    $"-f lavfi -i testsrc2=duration=0.001:size=256x144:rate=1 -c:v {codecName} -preset p1 -b:v 1k -f null -",

                var n when n.Contains("qsv") =>
                    isWindows
                        ? $"-init_hw_device qsv=hw -f lavfi -i testsrc2=duration=0.001:size=256x144:rate=1 " +
                          $"-vf hwupload=extra_hw_frames=4 -c:v {codecName} -preset ultrafast -b:v 1k -f null -"
                        : $"-init_hw_device vaapi=va:/dev/dri/renderD128 -init_hw_device qsv=hw@va -f lavfi " +
                          $"-i testsrc2=duration=0.001:size=256x144:rate=1 -vf format=nv12,hwupload=extra_hw_frames=4 " +
                          $"-c:v {codecName} -preset ultrafast -b:v 1k -f null -",

                var n when n.Contains("videotoolbox") =>
                    $"-f lavfi -i testsrc2=duration=0.001:size=256x144:rate=1 -c:v {codecName} -b:v 1k -f null -",

                _ =>
                    $"-f lavfi -i testsrc2=duration=0.001:size=256x144:rate=1 -c:v {codecName} -b:v 1k -f null -"
            };
        }

        private static string GetAudioTestCommand(string codecName)
            => $"-f lavfi -i sine=frequency=440:duration=0.1 -c:a {codecName} -f null -";

        // ─────────────────────────────────────────────────────────────
        //  PRIORIDADES
        // ─────────────────────────────────────────────────────────────

        private Dictionary<string, int> GetVideoCodecPriority() => new()
        {
            // NVIDIA
            { "av1_nvenc",        100 },
            { "h264_nvenc",        95 },
            { "hevc_nvenc",        90 },
            // AMD (AMF)
            { "av1_amf",           89 },
            { "h264_amf",          85 },
            { "hevc_amf",          80 },
            // Intel (QSV)
            { "av1_qsv",           79 },
            { "h264_qsv",          75 },
            { "hevc_qsv",          70 },
            // macOS (VideoToolbox)
            { "av1_videotoolbox",  69 },
            { "h264_videotoolbox", 65 },
            { "hevc_videotoolbox", 60 },
            // Linux (VAAPI)
            { "av1_vaapi",         59 },
            { "h264_vaapi",        55 },
            { "hevc_vaapi",        50 },
            // Windows (Media Foundation)
            { "h264_mf",           40 },
            { "hevc_mf",           35 },
            // Software (CPU)
            { "libsvtav1",         25 },
            { "libx264",           20 },
            { "libx265",           15 },
            { "libvpx-vp9",        10 },
            { "libaom-av1",         8 },
            { "librav1e",           7 },
            { "libx264rgb",         5 },
        };

        private Dictionary<string, int> GetAudioCodecPriority() => new()
        {
            { "aac",    100 },
            { "opus",    80 },
            { "flac",    60 },
            { "libmp3lame", 40 },
            { "vorbis",  20 },
        };

        // ─────────────────────────────────────────────────────────────
        //  ADD CODECS POR PLATAFORMA
        // ─────────────────────────────────────────────────────────────

        private void AddWindowsVideoCodecs(List<string> codecs, List<string> all, HardwareCapabilities hw)
        {
            if (hw.Nvidia) codecs.AddRange(all.Where(c => c.Contains("nvenc")));
            if (hw.AMD)    codecs.AddRange(all.Where(c => c.Contains("amf")));
            if (hw.Intel)  codecs.AddRange(all.Where(c => c.Contains("qsv")));
            if (hw.WindowsMediaFoundation) codecs.AddRange(all.Where(c => c.Contains("_mf")));
        }

        private void AddLinuxVideoCodecs(List<string> codecs, List<string> all, HardwareCapabilities hw)
        {
            if (hw.Intel)  codecs.AddRange(all.Where(c => c.Contains("qsv")));
            if (hw.VAAPI)  codecs.AddRange(all.Where(c => c.Contains("vaapi")));
            if (hw.Nvidia) codecs.AddRange(all.Where(c => c.Contains("nvenc")));
            codecs.AddRange(all.Where(c => c.Contains("v4l2m2m") || c.Contains("vulkan")));
        }

        private void AddMacVideoCodecs(List<string> codecs, List<string> all, HardwareCapabilities hw)
        {
            if (hw.VideoToolbox) codecs.AddRange(all.Where(c => c.Contains("videotoolbox")));
        }

        // ─────────────────────────────────────────────────────────────

        public void Dispose()
        {
            // Lazy<Task> no tiene recursos propios que liberar
        }
    }
}