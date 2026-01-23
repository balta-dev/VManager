// Services/Operations/ConvertOperation.cs
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FFMpegCore;
using VManager.Services.Core;
using VManager.Services.Models;
using VManager.Services.Core.Execution;
using VManager.Services.Core.Media;

namespace VManager.Services.Operations
{
    internal class ConvertOperation
    {
        private readonly IFFmpegExecutor _executor;
        private readonly IResumableFFmpegExecutor _resumableExecutor;
        private readonly IMediaAnalyzer _analyzer;

        public ConvertOperation(string ffmpegPath)
        {
            _executor = new FFmpegExecutor(ffmpegPath);
            _resumableExecutor = new ResumableFFmpegExecutor(ffmpegPath);
            _analyzer = new MediaAnalyzer();
        }
        
        // PARA TESTS
        public ConvertOperation(
            IFFmpegExecutor executor,
            IResumableFFmpegExecutor resumableExecutor,
            IMediaAnalyzer analyzer)
        {
            _executor = executor;
            _resumableExecutor = resumableExecutor;
            _analyzer = analyzer;
        }

        public async Task<ProcessingResult> ExecuteAsync(
            string inputPath,
            string outputPath,
            string? videoCodec,
            string? audioCodec,
            string selectedFormat,
            IProgress<IFFmpegProcessor.ProgressInfo> progress,
            CancellationToken cancellationToken = default)
        {
            inputPath = OutputPathBuilder.SanitizeFilename(inputPath);
            outputPath = OutputPathBuilder.SanitizeFilename(outputPath);
            
            // Defaults de códecs
            string selectedVideoCodec = videoCodec ?? "libx264";
            string selectedAudioCodec = audioCodec ?? "aac";

            // Caso especial: MOV sin códec especificado → DNxHR
            if (selectedFormat.Equals("mov", StringComparison.OrdinalIgnoreCase) && string.IsNullOrEmpty(videoCodec))
            {
                selectedVideoCodec = "dnxhd";
                selectedAudioCodec = "pcm_s24le";
            }

            // Análisis del video
            var analysisResult = await _analyzer.AnalyzeAsync(inputPath);
            if (!analysisResult.Success)
                return new ProcessingResult(false, analysisResult.Message);

            var mediaInfo = analysisResult.Result!;
            double duration = mediaInfo.Duration.TotalSeconds;

            // Detectar si necesitamos recodificar o solo cambiar contenedor
            bool needsReencoding = RequiresReencoding(mediaInfo, selectedVideoCodec, selectedAudioCodec);

            Console.WriteLine($"Conversión - Video: {selectedVideoCodec}, Audio: {selectedAudioCodec}, Formato: {selectedFormat}");
            Console.WriteLine($"[DEBUG] Recodificación necesaria: {needsReencoding}");

            // MODO RESUMABLE solo si recodifica Y es largo (>5 min)
            // Para conversiones simples (copy), siempre modo normal
            /*
             WIP - No está listo para release oficial todavía
             
            if (needsReencoding && duration > 300)
            {
                return await _resumableExecutor.ExecuteResumableAsync(
                    inputPath,
                    outputPath,
                    options =>
                    {
                        options
                            .WithCustomArgument("-map 0")
                            .WithVideoCodec(selectedVideoCodec)
                            .WithAudioCodec(selectedAudioCodec);

                        ApplySpecialCodecOptions(options, selectedVideoCodec);

                        return options;
                    },
                    duration,
                    progress,
                    cancellationToken
                );
            }
            */

            // Modo normal (rápido)
            var args = FFMpegArguments
                .FromFileInput(inputPath)
                .OutputToFile(outputPath, overwrite: true, options =>
                {
                    options.WithCustomArgument("-map 0");
                    
                    if (needsReencoding)
                    {
                        options
                            .WithVideoCodec(selectedVideoCodec)
                            .WithAudioCodec(selectedAudioCodec);

                        ApplySpecialCodecOptions(options, selectedVideoCodec);
                    }
                    else
                    {
                        // Solo cambiar contenedor, sin recodificar (ULTRA RÁPIDO)
                        options.WithCustomArgument("-c copy");
                    }
                });

            return await _executor.ExecuteAsync(
                inputPath,
                outputPath,
                args,
                duration,
                progress,
                cancellationToken
            );
        }

        // Detecta si necesitamos recodificar o solo cambiar contenedor
        private bool RequiresReencoding(IMediaAnalysis mediaInfo, string targetVideoCodec, string targetAudioCodec)
        {
            var videoStream = mediaInfo.PrimaryVideoStream;
            var audioStream = mediaInfo.PrimaryAudioStream;

            if (videoStream == null || audioStream == null)
                return true; // Sin streams, recodificar por seguridad

            // Normalizar nombres de códecs
            string currentVideoCodec = videoStream.CodecName?.ToLower() ?? "";
            string currentAudioCodec = audioStream.CodecName?.ToLower() ?? "";
            string targetVideo = targetVideoCodec.ToLower();
            string targetAudio = targetAudioCodec.ToLower();

            // Mapeo de códecs equivalentes
            bool videoMatches = 
                (currentVideoCodec == targetVideo) ||
                (currentVideoCodec == "h264" && targetVideo == "libx264") ||
                (currentVideoCodec == "hevc" && targetVideo == "libx265") ||
                (currentVideoCodec == "vp9" && targetVideo == "libvpx-vp9");

            bool audioMatches = 
                (currentAudioCodec == targetAudio) ||
                (currentAudioCodec == "aac" && targetAudio == "aac");

            // Si ambos coinciden, solo necesitamos cambiar el contenedor
            return !(videoMatches && audioMatches);
        }

        // Método privado para evitar duplicación de lógica DNxHR
        private static void ApplySpecialCodecOptions(FFMpegArgumentOptions options, string videoCodec)
        {
            if (HardwareAccelerationConfigurator.IsDNxHRCodec(videoCodec))
            {
                options
                    .WithCustomArgument("-profile:v dnxhr_hq")
                    .WithCustomArgument("-pix_fmt yuv422p");
            }
            else
            {
                options.WithAudioBitrate(128);
                HardwareAccelerationConfigurator.Configure(options, videoCodec);
            }
        }
    }
}