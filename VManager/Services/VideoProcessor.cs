using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FFMpegCore;

namespace VManager.Services
{
    public class VideoProcessor : IVideoProcessor
    {
        private readonly string _ffmpegPath = FFmpegManager.FfmpegPath;

        public static class ErrorMessages
        {
            public const string FileNotFound = "Archivo no encontrado.";
            public const string InvalidPercentage = "Porcentaje inválido.";
            public const string AnalysisError = "Error al analizar el video: {0}";
            public const string InvalidDuration = "Error al obtener duración.";
            public const string InvalidCutParameters = "Parámetros de corte inválidos.";
            public const string InvalidAudioFormat = "Formato de audio no especificado.";
            public const string NoAudioStream = "El archivo no contiene pistas de audio.";
        }

        private (string videoCodec, string audioCodec) GetDefaultCodecs(string? videoCodec, string? audioCodec)
        {
            return (videoCodec ?? "libx264", audioCodec ?? "aac");
        }

        private async Task<AnalysisResult<IMediaAnalysis>> AnalyzeVideoAsync(string inputPath)
        {
            try
            {
                if (!File.Exists(inputPath))
                    return new AnalysisResult<IMediaAnalysis>(false, ErrorMessages.FileNotFound);

                var mediaInfo = await FFProbe.AnalyseAsync(inputPath);
                if (mediaInfo.Duration.TotalSeconds <= 0)
                    return new AnalysisResult<IMediaAnalysis>(false, ErrorMessages.InvalidDuration);

                return new AnalysisResult<IMediaAnalysis>(true, "Análisis completado", mediaInfo);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG]: Error: {ex.Message}");
                Console.WriteLine($"[DEBUG]: Stack Trace: {ex.StackTrace}");
                return new AnalysisResult<IMediaAnalysis>(false, string.Format(ErrorMessages.AnalysisError, ex.Message));
            }
        }

        private async Task<ProcessingResult> ExecuteFFmpegProcessAsync(
            string inputPath,
            string outputPath,
            FFMpegArgumentProcessor args,
            double duration,
            IProgress<double> progress,
            CancellationToken cancellationToken)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    Arguments = args.Arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            try
            {
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    cts.Token.Register(() =>
                    {
                        if (!process.HasExited)
                        {
                            process.Kill();
                            Console.WriteLine("[DEBUG]: Proceso FFmpeg terminado por cancelación.");
                            if (File.Exists(outputPath))
                            {
                                try
                                {
                                    File.Delete(outputPath);
                                    Console.WriteLine("[DEBUG]: Archivo de salida eliminado tras cancelación.");
                                }
                                catch (IOException ex)
                                {
                                    Console.WriteLine($"[DEBUG]: No se pudo eliminar el archivo: {ex.Message}");
                                }
                            }
                        }
                    });

                    process.Start();

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
                                    progress.Report(Math.Min(time.TotalSeconds / duration, 1.0));
                                }
                            }
                        }
                    }

                    await process.WaitForExitAsync(cts.Token);

                    if (cts.Token.IsCancellationRequested)
                    {
                        if (File.Exists(outputPath))
                            File.Delete(outputPath);
                        return new ProcessingResult(false, "Operación cancelada por el usuario.");
                    }

                    if (process.ExitCode != 0)
                    {
                        string? errorOutput = await process.StandardError.ReadToEndAsync();
                        if (errorOutput.Contains("No such file or directory") ||
                            errorOutput.Contains("Permission denied") ||
                            errorOutput.Contains("Could not create") ||
                            errorOutput.Contains("Invalid argument"))
                        {
                            throw new Exception($"FFmpeg error: {errorOutput} (ExitCode: {process.ExitCode})");
                        }
                    }

                    if (!File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
                    {
                        if (File.Exists(outputPath))
                            File.Delete(outputPath);
                        return new ProcessingResult(false, "El archivo de salida no se creó correctamente.");
                    }

                    return new ProcessingResult(true, "¡Operación finalizada!", outputPath);
                }
            }
            catch (OperationCanceledException)
            {
                if (File.Exists(outputPath))
                    File.Delete(outputPath);
                return new ProcessingResult(false, "Operación cancelada por el usuario.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG]: Error: {ex.Message}");
                return new ProcessingResult(false, $"Error: {ex.Message}");
            }
        }

        public async Task<ProcessingResult> CutAsync(
            string inputPath,
            string outputPath,
            TimeSpan start,
            TimeSpan duration,
            IProgress<double> progress,
            CancellationToken cancellationToken = default)
        {
            var analysisResult = await AnalyzeVideoAsync(inputPath);
            if (!analysisResult.Success)
                return new ProcessingResult(false, analysisResult.Message);

            var mediaInfo = analysisResult.Result!;
            double totalDuration = mediaInfo.Duration.TotalSeconds;

            string directory = Path.GetDirectoryName(inputPath)!;
            string fileName = Path.GetFileNameWithoutExtension(inputPath);
            string extension = Path.GetExtension(inputPath);
            outputPath = Path.Combine(directory, $"{fileName}-VCUT{extension}");

            string? warningMessage = null;
            if (start < TimeSpan.Zero || duration <= TimeSpan.Zero)
                return new ProcessingResult(false, ErrorMessages.InvalidCutParameters);

            if (start.TotalSeconds + duration.TotalSeconds > totalDuration)
            {
                duration = TimeSpan.FromSeconds(totalDuration - start.TotalSeconds);
                warningMessage = $"Nota: La duración del corte se ajustó automáticamente a {duration:hh\\:mm\\:ss}.";
            }

            Console.WriteLine($"Corte - Video: copy, Audio: copy, Inicio: {start}, Duración: {duration:hh\\:mm\\:ss}");

            var args = FFMpegArguments
                .FromFileInput(inputPath, false, options => options.Seek(start))
                .OutputToFile(outputPath, overwrite: true, options =>
                    options
                        .WithVideoCodec("copy")
                        .WithAudioCodec("copy")
                        .WithDuration(duration));

            var result = await ExecuteFFmpegProcessAsync(inputPath, outputPath, args, duration.TotalSeconds, progress, cancellationToken);
            if (result.Success && !string.IsNullOrEmpty(warningMessage))
                return new ProcessingResult(true, result.Message, result.OutputPath, warningMessage);

            return result;
        }

        public async Task<ProcessingResult> CompressAsync(
            string inputPath,
            string outputPath,
            int compressionPercentage,
            string? videoCodec,
            string? audioCodec,
            IProgress<double> progress,
            CancellationToken cancellationToken = default)
        {
            if (compressionPercentage <= 0 || compressionPercentage > 100)
                return new ProcessingResult(false, ErrorMessages.InvalidPercentage);

            var (selectedVideoCodec, selectedAudioCodec) = GetDefaultCodecs(videoCodec, audioCodec);
            var analysisResult = await AnalyzeVideoAsync(inputPath);
            if (!analysisResult.Success)
                return new ProcessingResult(false, analysisResult.Message);

            string extension = Path.GetExtension(inputPath);
            string outputFileName = Path.GetFileNameWithoutExtension(inputPath) + $"-{compressionPercentage}{extension}";
            outputPath = Path.Combine(Path.GetDirectoryName(inputPath)!, outputFileName);

            var mediaInfo = analysisResult.Result!;
            double duration = mediaInfo.Duration.TotalSeconds;
            long sizeBytes = new FileInfo(inputPath).Length;
            long targetSize = sizeBytes * compressionPercentage / 100;
            int targetBitrate = (int)((targetSize * 8) / duration / 1000);
            targetBitrate = Math.Max(targetBitrate, 100);

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
                });

            return await ExecuteFFmpegProcessAsync(inputPath, outputPath, args, duration, progress, cancellationToken);
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
                return new ProcessingResult(false, analysisResult.Message);

            string outputFileName = Path.GetFileNameWithoutExtension(inputPath) + $"-VCONV.{selectedFormat}";
            outputPath = Path.Combine(Path.GetDirectoryName(inputPath)!, outputFileName);

            var mediaInfo = analysisResult.Result!;
            double duration = mediaInfo.Duration.TotalSeconds;

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

            return await ExecuteFFmpegProcessAsync(inputPath, outputPath, args, duration, progress, cancellationToken);
        }

        public async Task<ProcessingResult> AudiofyAsync(
            string inputPath,
            string outputPath,
            string? videoCodec,
            string? audioCodec,
            string selectedAudioFormat,
            IProgress<double> progress,
            CancellationToken cancellationToken = default)
        {
            Console.WriteLine($"[DEBUG] Audiofy - Origen: {inputPath}, Formato: '{selectedAudioFormat}'");

            if (string.IsNullOrEmpty(selectedAudioFormat))
                return new ProcessingResult(false, ErrorMessages.InvalidAudioFormat);

            var analysisResult = await AnalyzeVideoAsync(inputPath);
            if (!analysisResult.Success)
                return new ProcessingResult(false, analysisResult.Message);

            var mediaInfo = analysisResult.Result!;
            if (mediaInfo.AudioStreams.Count == 0)
                return new ProcessingResult(false, ErrorMessages.NoAudioStream);

            var originalAudioStream = mediaInfo.AudioStreams.First();
            string originalCodec = originalAudioStream.CodecName?.ToLower() ?? "unknown";
            double duration = mediaInfo.Duration.TotalSeconds;

            var processingDecision = DecideAudioProcessing(originalCodec, selectedAudioFormat);
            string normalizedFormat = NormalizeFormatName(selectedAudioFormat);
            string outputFileName = Path.GetFileNameWithoutExtension(inputPath) + $"-ACONV.{normalizedFormat}";
            outputPath = Path.Combine(Path.GetDirectoryName(inputPath)!, outputFileName);

            Console.WriteLine($"[DEBUG] Decisión: {processingDecision.Action}, Códec: {processingDecision.Codec}, Archivo: {outputPath}");

            var args = FFMpegArguments
                .FromFileInput(inputPath)
                .OutputToFile(outputPath, overwrite: true, options =>
                {
                    options.WithCustomArgument("-vn");
                    if (processingDecision.Action == AudioProcessingAction.Copy)
                        options.WithAudioCodec("copy");
                    else
                        options
                            .WithAudioCodec(processingDecision.Codec)
                            .WithAudioBitrate(processingDecision.Bitrate);
                });

            var result = await ExecuteFFmpegProcessAsync(inputPath, outputPath, args, duration, progress, cancellationToken);
            if (result.Success)
            {
                string successMessage = processingDecision.Action == AudioProcessingAction.Copy
                    ? $"¡Audio extraído sin recodificar! ({processingDecision.Reason})"
                    : "¡Audio extraído y convertido!";
                return new ProcessingResult(true, successMessage, result.OutputPath);
            }

            return result;
        }

        private struct AudioProcessingDecision
        {
            public AudioProcessingAction Action;
            public string Codec;
            public int Bitrate;
            public string Reason;
        }

        private enum AudioProcessingAction
        {
            Copy,
            Reencode
        }

        private AudioProcessingDecision DecideAudioProcessing(string originalCodec, string targetFormat)
        {
            var decision = new AudioProcessingDecision();
            string normalizedFormat = NormalizeFormatName(targetFormat);

            var compatibleMappings = new Dictionary<string, HashSet<string>>
            {
                ["aac"] = new HashSet<string> { "aac", "m4a", "mp4" },
                ["mp3"] = new HashSet<string> { "mp3" },
                ["flac"] = new HashSet<string> { "flac" },
                ["vorbis"] = new HashSet<string> { "ogg", "oga" },
                ["opus"] = new HashSet<string> { "opus", "ogg" },
                ["pcm_s16le"] = new HashSet<string> { "wav" },
                ["pcm_s24le"] = new HashSet<string> { "wav" },
                ["pcm_f32le"] = new HashSet<string> { "wav" }
            };

            if (compatibleMappings.ContainsKey(originalCodec) && compatibleMappings[originalCodec].Contains(normalizedFormat))
            {
                decision.Action = AudioProcessingAction.Copy;
                decision.Codec = "copy";
                decision.Bitrate = 0;
                decision.Reason = $"Códec {originalCodec} compatible con formato {normalizedFormat}";
                return decision;
            }

            decision.Action = AudioProcessingAction.Reencode;
            decision.Reason = $"Códec {originalCodec} no compatible con {normalizedFormat}, recodificando";

            switch (normalizedFormat)
            {
                case "mp3":
                    decision.Codec = "libmp3lame";
                    decision.Bitrate = 192;
                    break;
                case "aac":
                case "m4a":
                    decision.Codec = "aac";
                    decision.Bitrate = 128;
                    break;
                case "ogg":
                case "oga":
                    decision.Codec = "libvorbis";
                    decision.Bitrate = 192;
                    break;
                case "flac":
                    decision.Codec = "flac";
                    decision.Bitrate = 0;
                    break;
                case "wav":
                    decision.Codec = "pcm_s16le";
                    decision.Bitrate = 0;
                    break;
                case "opus":
                    decision.Codec = "libopus";
                    decision.Bitrate = 128;
                    break;
                case "wma":
                    decision.Codec = "wmav2";
                    decision.Bitrate = 128;
                    break;
                default:
                    decision.Codec = "aac";
                    decision.Bitrate = 128;
                    break;
            }

            return decision;
        }

        private string NormalizeFormatName(string input)
        {
            var codecToFormat = new Dictionary<string, string>
            {
                ["libmp3lame"] = "mp3",
                ["libvorbis"] = "ogg",
                ["libopus"] = "opus",
                ["pcm_s16le"] = "wav",
                ["pcm_s24le"] = "wav",
                ["pcm_f32le"] = "wav",
                ["wmav2"] = "wma"
            };

            string normalized = input.ToLower();
            return codecToFormat.ContainsKey(normalized) ? codecToFormat[normalized] : normalized;
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
                case "h264_nvenc":
                case "hevc_nvenc":
                case "av1_nvenc":
                    opts.WithCustomArgument("-preset fast")
                        .WithCustomArgument("-profile:v main")
                        .WithCustomArgument("-rc vbr")
                        .WithCustomArgument("-gpu 0");
                    break;

                case "h264_amf":
                case "hevc_amf":
                case "av1_amf":
                    opts.WithCustomArgument("-usage transcoding")
                        .WithCustomArgument("-profile:v main")
                        .WithCustomArgument("-quality speed")
                        .WithCustomArgument("-rc vbr_peak");
                    break;

                case "h264_qsv":
                case "hevc_qsv":
                case "av1_qsv":
                    opts.WithCustomArgument("-preset fast")
                        .WithCustomArgument("-profile:v main")
                        .WithCustomArgument("-look_ahead 0");
                    break;

                case "h264_mf":
                case "hevc_mf":
                    break;

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
                case "h264_vaapi":
                case "hevc_vaapi":
                case "vp8_vaapi":
                case "vp9_vaapi":
                case "av1_vaapi":
                    opts.WithCustomArgument("-vaapi_device /dev/dri/renderD128")
                        .WithCustomArgument("-vf format=nv12,hwupload");
                    if (codec == "h264_vaapi" || codec == "hevc_vaapi")
                        opts.WithCustomArgument("-profile:v main");
                    break;

                case "h264_nvenc":
                case "hevc_nvenc":
                case "av1_nvenc":
                    opts.WithCustomArgument("-preset fast")
                        .WithCustomArgument("-profile:v main")
                        .WithCustomArgument("-rc vbr")
                        .WithCustomArgument("-gpu 0");
                    break;

                case "h264_v4l2m2m":
                case "vp8_v4l2m2m":
                    opts.WithCustomArgument("-pix_fmt yuv420p");
                    break;

                case "h264_vulkan":
                case "hevc_vulkan":
                    opts.WithCustomArgument("-vulkan_params device=0");
                    break;

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
                case "h264_videotoolbox":
                case "hevc_videotoolbox":
                    opts.WithCustomArgument("-profile:v main")
                        .WithCustomArgument("-pix_fmt yuv420p")
                        .WithCustomArgument("-realtime 0");
                    break;

                case "prores_videotoolbox":
                    opts.WithCustomArgument("-profile:v 2");
                    break;

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