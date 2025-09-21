using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FFMpegCore;

namespace VManager.Services
{
    public class VideoProcessor : IVideoProcessor
    {
        public static class ErrorMessages
        {
            public const string FileNotFound = "Archivo no encontrado.";
            public const string InvalidPercentage = "Porcentaje inválido.";
            public const string AnalysisError = "Error al analizar el video: {0}";
            public const string InvalidDuration = "Error al obtener duración.";
            public const string InvalidCutParameters = "Parámetros de corte inválidos.";
        }

        private (string videoCodec, string audioCodec) GetDefaultCodecs(string? videoCodec, string? audioCodec)
        {
            return (videoCodec ?? "libx264", audioCodec ?? "aac");
        }

        private static async Task<AnalysisResult<IMediaAnalysis>> AnalyzeVideoAsync(string inputPath)
        {
            try
            {
                if (!File.Exists(inputPath))
                    return new AnalysisResult<IMediaAnalysis>(false, ErrorMessages.FileNotFound);
                
                var mediaInfo = await FFProbe.AnalyseAsync(inputPath);
                double duration = mediaInfo.Duration.TotalSeconds;
                if (duration <= 0)
                {
                    return new AnalysisResult<IMediaAnalysis>(false, ErrorMessages.InvalidDuration);
                }
                return new AnalysisResult<IMediaAnalysis>(true, "Análisis completado", mediaInfo);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG]: Error: {ex.Message}");
                Console.WriteLine($"[DEBUG]: Stack Trace: {ex.StackTrace}");
                return new AnalysisResult<IMediaAnalysis>(false, string.Format(ErrorMessages.AnalysisError, ex.Message));
            }
        }
        
        public async Task<ProcessingResult> CutAsync(
            string inputPath,
            string outputPath,
            TimeSpan start,
            TimeSpan duration,
            IProgress<double> progress)
        {
            var analysisResult = await AnalyzeVideoAsync(inputPath);
            if (!analysisResult.Success)
            {
                return new ProcessingResult(false, analysisResult.Message);
            }

            var mediaInfo = analysisResult.Result!;
            double totalDuration = mediaInfo.Duration.TotalSeconds;
            
            string directory = Path.GetDirectoryName(inputPath)!;
            string fileName = Path.GetFileNameWithoutExtension(inputPath);
            string extension = Path.GetExtension(inputPath); 
            outputPath = Path.Combine(directory, $"{fileName}-VCUT{extension}");

            // Validar parámetros de corte
            string warningMessage = null;
            if (start < TimeSpan.Zero || duration <= TimeSpan.Zero)
            {
                return new ProcessingResult(false, ErrorMessages.InvalidCutParameters);
            }
            if (start.TotalSeconds + duration.TotalSeconds > totalDuration)
            {
                // Ajustar duration para que no exceda el video
                duration = TimeSpan.FromSeconds(totalDuration - start.TotalSeconds);
                string formatted = duration.ToString(@"hh\:mm\:ss");
                warningMessage = $"Nota: La duración del corte se ajustó automáticamente a {formatted}.";
            }
            try
            {
                Console.WriteLine($"Corte - Video: copy, Audio: copy, Inicio: {start}, Duración: {duration.ToString(@"hh\:mm\:ss")}");

                var args = FFMpegArguments
                    .FromFileInput(inputPath, false, options => options.Seek(start))
                    .OutputToFile(outputPath, overwrite: true, options =>
                    {
                        options
                            .WithVideoCodec("copy")
                            .WithAudioCodec("copy")
                            .WithDuration(duration);
                    })
                    .NotifyOnProgress(time =>
                    {
                        progress.Report(time.TotalSeconds / duration.TotalSeconds);
                    });

                await args.ProcessAsynchronously();
                
                if (!string.IsNullOrEmpty(warningMessage))
                {
                    return new ProcessingResult(true, "¡Corte finalizado!", outputPath, warningMessage);
                }
                
                return new ProcessingResult(true, "¡Corte finalizado!", outputPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG]: Error: {ex.Message}");
                Console.WriteLine($"[DEBUG]: Stack Trace: {ex.StackTrace}");
                return new ProcessingResult(false, $"Error: {ex.Message}");
            }
        }

        public async Task<ProcessingResult> CompressAsync(
            string inputPath,
            string outputPath,
            int compressionPercentage,
            string? videoCodec,
            string? audioCodec,
            IProgress<double> progress)
        {
            var (selectedVideoCodec, selectedAudioCodec) = GetDefaultCodecs(videoCodec, audioCodec);

            var analysisResult = await AnalyzeVideoAsync(inputPath);
            if (!analysisResult.Success)
            {
                return new ProcessingResult(false, analysisResult.Message);
            }
            
            string extension = Path.GetExtension(inputPath);
            string outputFileName = Path.GetFileNameWithoutExtension(inputPath) + $"-{compressionPercentage}{extension}";
            outputPath = Path.Combine(Path.GetDirectoryName(inputPath)!, outputFileName);
            
            var mediaInfo = analysisResult.Result!;
            double duration = mediaInfo.Duration.TotalSeconds;

            long sizeBytes = new FileInfo(inputPath).Length;
            long targetSize = sizeBytes * compressionPercentage / 100;
            int targetBitrate = (int)((targetSize * 8) / duration / 1000);
            targetBitrate = Math.Max(targetBitrate, 100);

            try
            {
                Console.WriteLine($"Compresión - Video: {selectedVideoCodec}, Audio: {selectedAudioCodec}, Bitrate: {targetBitrate} kbps");

                var args = FFMpegArguments
                    .FromFileInput(inputPath)
                    .OutputToFile(outputPath, overwrite: true, options =>
                    {
                        options
                            .WithVideoCodec(selectedVideoCodec)
                            .WithVideoBitrate(targetBitrate)
                            .WithAudioCodec(selectedAudioCodec)
                            .WithAudioBitrate(128);
                        ConfigureHardwareAcceleration(options, selectedVideoCodec);
                    })
                    .NotifyOnProgress(time =>
                    {
                        progress.Report(time.TotalSeconds / duration);
                    });

                await args.ProcessAsynchronously();
                return new ProcessingResult(true, "¡Compresión finalizada!", outputPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG]: Error: {ex.Message}");
                return new ProcessingResult(false, $"Error: {ex.Message}");
            }
        }
        
        public async Task<ProcessingResult> ConvertAsync(
            string inputPath,
            string outputPath,
            string? videoCodec,
            string? audioCodec,
            string selectedFormat,
            IProgress<double> progress,
            CancellationToken cancellationToken = default)
        {
            var (selectedVideoCodec, selectedAudioCodec) = GetDefaultCodecs(videoCodec, audioCodec);

            var analysisResult = await AnalyzeVideoAsync(inputPath);
            if (!analysisResult.Success)
            {
                return new ProcessingResult(false, analysisResult.Message);
            }

            string outputFileName = Path.GetFileNameWithoutExtension(inputPath) + $"-VCONV.{selectedFormat}";
            outputPath = Path.Combine(Path.GetDirectoryName(inputPath)!, outputFileName);

            var mediaInfo = analysisResult.Result!;
            double duration = mediaInfo.Duration.TotalSeconds;

            try
            {
                Console.WriteLine($"Conversión - Video: {selectedVideoCodec}, Audio: {selectedAudioCodec}, Extension: {selectedFormat}");

                var args = FFMpegArguments
                    .FromFileInput(inputPath)
                    .OutputToFile(outputPath, overwrite: true, options =>
                    {
                        options
                            .WithVideoCodec(selectedVideoCodec)
                            .WithAudioCodec(selectedAudioCodec)
                            .WithAudioBitrate(128);
                        ConfigureHardwareAcceleration(options, selectedVideoCodec);
                    });

                // Configurar el proceso manualmente
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "ffmpeg", // Asegúrate de que FFmpeg esté en el PATH
                        Arguments = args.Arguments, // Obtener los argumentos generados
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    // Registrar la acción de cancelación
                    cts.Token.Register(() =>
                    {
                        if (!process.HasExited)
                        {
                            process.Kill();
                            Console.WriteLine("[DEBUG]: Proceso FFmpeg terminado por cancelación.");
                        }
                    });

                    process.Start();

                    // Leer el progreso desde stderr
                    using (var reader = process.StandardError)
                    {
                        string? line;
                        while ((line = await reader.ReadLineAsync()) != null)
                        {
                            if (line.Contains("time="))
                            {
                                var timeMatch = Regex.Match(line, @"time=(\d{2}:\d{2}:\d{2}\.\d{2})");
                                if (timeMatch.Success && TimeSpan.TryParse(timeMatch.Groups[1].Value, out var time))
                                {
                                    progress.Report(time.TotalSeconds / duration);
                                }
                            }
                        }
                    }

                    await process.WaitForExitAsync();

                    if (cts.Token.IsCancellationRequested)
                    {
                        if (File.Exists(outputPath))
                        {
                            File.Delete(outputPath);
                            Console.WriteLine("[DEBUG]: Archivo de salida eliminado tras cancelación.");
                        }
                        Console.WriteLine("[DEBUG]: Conversión cancelada por el usuario.");
                        return new ProcessingResult(false, "Conversión cancelada por el usuario.");
                    }

                    if (process.ExitCode != 0 && !cts.Token.IsCancellationRequested)
                    {
                        throw new Exception("FFmpeg terminó con un error.");
                    }
                }

                return new ProcessingResult(true, "¡Conversión finalizada!", outputPath);
            }
            catch (OperationCanceledException)
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                    Console.WriteLine("[DEBUG]: Archivo de salida eliminado tras cancelación.");
                }
                Console.WriteLine("[DEBUG]: Conversión cancelada por el usuario.");
                return new ProcessingResult(false, "Conversión cancelada por el usuario.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG]: Error: {ex.Message}");
                return new ProcessingResult(false, $"Error: {ex.Message}");
            }
        }

        private void ConfigureHardwareAcceleration(FFMpegArgumentOptions opts, string videoCodec)
        {
            var codec = videoCodec.ToLower();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ConfigureWindowsAcceleration(opts, codec);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                ConfigureLinuxAcceleration(opts, codec);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                ConfigureMacAcceleration(opts, codec);
            }
        }

        private void ConfigureWindowsAcceleration(FFMpegArgumentOptions opts, string codec)
        {
            switch (codec)
            {
                // NVIDIA NVENC
                case "h264_nvenc":
                case "hevc_nvenc":
                case "av1_nvenc":
                    opts.WithCustomArgument("-preset fast")
                        .WithCustomArgument("-profile:v main")
                        .WithCustomArgument("-rc vbr")
                        .WithCustomArgument("-gpu 0");
                    break;

                // AMD AMF
                case "h264_amf":
                case "hevc_amf":
                case "av1_amf":
                    opts.WithCustomArgument("-usage transcoding")
                        .WithCustomArgument("-profile:v main")
                        .WithCustomArgument("-quality speed")
                        .WithCustomArgument("-rc vbr_peak");
                    break;

                // Intel QuickSync
                case "h264_qsv":
                case "hevc_qsv":
                case "av1_qsv":
                    opts.WithCustomArgument("-preset fast")
                        .WithCustomArgument("-profile:v main")
                        .WithCustomArgument("-look_ahead 0");
                    break;

                // Windows Media Foundation
                case "h264_mf":
                case "hevc_mf":
                    break;

                // Software codecs - configuración optimizada
                case "libx264":
                    opts.WithCustomArgument("-preset fast")
                        .WithCustomArgument("-profile:v main");
                    break;

                case "libx265":
                    opts.WithCustomArgument("-preset fast")
                        .WithCustomArgument("-profile:v main");
                    break;
            }
        }

        private void ConfigureLinuxAcceleration(FFMpegArgumentOptions opts, string codec)
        {
            switch (codec)
            {
                // VAAPI (AMD/Intel)
                case "h264_vaapi":
                case "hevc_vaapi":
                case "vp8_vaapi":
                case "vp9_vaapi":
                case "av1_vaapi":
                    opts.WithCustomArgument("-vaapi_device /dev/dri/renderD128")
                        .WithCustomArgument("-vf format=nv12,hwupload");

                    if (codec == "h264_vaapi" || codec == "hevc_vaapi")
                    {
                        opts.WithCustomArgument("-profile:v main");
                    }
                    break;

                // NVIDIA NVENC en Linux
                case "h264_nvenc":
                case "hevc_nvenc":
                case "av1_nvenc":
                    opts.WithCustomArgument("-preset fast")
                        .WithCustomArgument("-profile:v main")
                        .WithCustomArgument("-rc vbr")
                        .WithCustomArgument("-gpu 0");
                    break;

                // V4L2 Memory-to-Memory (Raspberry Pi, ARM, etc.)
                case "h264_v4l2m2m":
                case "vp8_v4l2m2m":
                    opts.WithCustomArgument("-pix_fmt yuv420p");
                    break;

                // Vulkan
                case "h264_vulkan":
                case "hevc_vulkan":
                    opts.WithCustomArgument("-vulkan_params device=0");
                    break;

                // Software codecs optimizados
                case "libx264":
                    opts.WithCustomArgument("-preset fast")
                        .WithCustomArgument("-profile:v main");
                    break;

                case "libx265":
                    opts.WithCustomArgument("-preset fast")
                        .WithCustomArgument("-profile:v main");
                    break;
            }
        }

        private void ConfigureMacAcceleration(FFMpegArgumentOptions opts, string codec)
        {
            switch (codec)
            {
                // VideoToolbox
                case "h264_videotoolbox":
                case "hevc_videotoolbox":
                    opts.WithCustomArgument("-profile:v main")
                        .WithCustomArgument("-pix_fmt yuv420p")
                        .WithCustomArgument("-realtime 0");
                    break;

                case "prores_videotoolbox":
                    opts.WithCustomArgument("-profile:v 2"); // ProRes 422
                    break;

                // Software codecs
                case "libx264":
                    opts.WithCustomArgument("-preset fast")
                        .WithCustomArgument("-profile:v main");
                    break;

                case "libx265":
                    opts.WithCustomArgument("-preset fast")
                        .WithCustomArgument("-profile:v main");
                    break;
            }
        }
    }

    public class ProcessingResult
    {
        public bool Success { get; }
        public string Message { get; }
        public string OutputPath { get; }
        public string Warning { get; }

        public ProcessingResult(bool success, string message, string outputPath = null, string warning = null)
        {
            Success = success;
            Message = message;
            OutputPath = outputPath;
            Warning = warning;
        }
    }

    public class AnalysisResult<T>
    {
        public bool Success { get; }
        public string Message { get; }
        public T? Result { get; }

        public AnalysisResult(bool success, string message, T? result = default)
        {
            Success = success;
            Message = message;
            Result = result;
        }
    }
}