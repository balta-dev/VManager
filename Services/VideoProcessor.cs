using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using FFMpegCore;

namespace VManager.Services
{
    public class VideoProcessor : IVideoProcessor
    {
        public async Task<ProcessingResult> CutAsync(
            string inputPath,
            string outputPath,
            TimeSpan start,
            TimeSpan duration,
            string videoCodec,
            string audioCodec,
            IProgress<double> progress)
        {
            if (!File.Exists(inputPath))
                return new ProcessingResult(false, "Archivo no encontrado.");

            var mediaInfo = await FFProbe.AnalyseAsync(inputPath);
            if (mediaInfo.Duration.TotalSeconds <= 0)
                return new ProcessingResult(false, "Error al obtener duración.");

            try
            {
                var args = FFMpegArguments
                    .FromFileInput(inputPath)
                    .OutputToFile(outputPath, overwrite: true, options =>
                    {
                        options
                            .WithVideoCodec(videoCodec)
                            .WithAudioCodec(audioCodec)
                            .WithAudioBitrate(128);

                        // Aquí aplicás la aceleración según el codec
                        ConfigureHardwareAcceleration(options, videoCodec);
                    })
                    .NotifyOnProgress(time =>
                    {
                        progress.Report(time.TotalSeconds / duration.TotalSeconds);
                    });

                await args.ProcessAsynchronously();
                return new ProcessingResult(true, "Corte realizado correctamente.", outputPath);
            }
            catch (Exception ex)
            {
                return new ProcessingResult(false, $"[DEBUG]: Error: {ex.Message}");
            }
        }

        public async Task<ProcessingResult> CompressAsync(
            string inputPath,
            string outputPath,
            int compressionPercentage,
            string videoCodec,
            string audioCodec,
            IProgress<double> progress)
        {
            if (!File.Exists(inputPath))
                return new ProcessingResult(false, "Archivo no encontrado.");
           
            IMediaAnalysis mediaInfo;
            try
            {
                mediaInfo = await FFProbe.AnalyseAsync(inputPath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG]: Error FFPROBE: {ex.Message}");
                Console.WriteLine($"[DEBUG]: Stack Trace: {ex.StackTrace}");
                return new ProcessingResult(false, $"Error al analizar el video: {ex.Message}");
            }
            
            double duration = mediaInfo.Duration.TotalSeconds;
            if (duration <= 0)
                return new ProcessingResult(false, "Error al obtener duración.");

            long sizeBytes = new FileInfo(inputPath).Length;
            long targetSize = sizeBytes * compressionPercentage / 100;
            int targetBitrate = (int)((targetSize * 8) / duration / 1000);

            // Asegurar bitrate mínimo razonable
            targetBitrate = Math.Max(targetBitrate, 100);

            try
            {
                Console.WriteLine($"Compresión - Video: {videoCodec}, Audio: {audioCodec}, Bitrate: {targetBitrate} kbps");
                
                var args = FFMpegArguments
                    .FromFileInput(inputPath)
                    .OutputToFile(outputPath, overwrite: true, options =>
                    {
                        options
                            .WithVideoCodec(videoCodec)
                            .WithVideoBitrate(targetBitrate)
                            .WithAudioCodec(audioCodec)
                            .WithAudioBitrate(128);

                        // Aquí aplicás la aceleración según el codec
                        ConfigureHardwareAcceleration(options, videoCodec);
                    })
                    .NotifyOnProgress(time =>
                    {
                        progress.Report(time.TotalSeconds / duration);
                    });
                
                await args.ProcessAsynchronously();
                return new ProcessingResult(true, $"¡Compresión finalizada!\nArchivo: {outputPath}", outputPath);

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
                    opts.WithCustomArgument("-profile:v main");
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

        public ProcessingResult(bool success, string message, string outputPath = null)
        {
            Success = success;
            Message = message;
            OutputPath = outputPath;
        }
    }
}