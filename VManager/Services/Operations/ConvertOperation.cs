// Services/Operations/ConvertOperation.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using FFMpegCore;
using VManager.Services.Core; // Para HardwareAccelerationConfigurator, FFmpegExecutor, ResumableFFmpegExecutor
using VManager.Services.Models;
using VManager.Services.Core.Execution;
using VManager.Services.Core.Media; // Para ProcessingResult

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

            double duration = analysisResult.Result!.Duration.TotalSeconds;

            Console.WriteLine($"Conversión - Video: {selectedVideoCodec}, Audio: {selectedAudioCodec}, Formato: {selectedFormat}");

            // MODO RESUMABLE para videos largos (>5 minutos)
            if (duration > 300)
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

            // Modo normal
            var args = FFMpegArguments
                .FromFileInput(inputPath)
                .OutputToFile(outputPath, overwrite: true, options =>
                {
                    options
                        .WithCustomArgument("-map 0")
                        .WithVideoCodec(selectedVideoCodec)
                        .WithAudioCodec(selectedAudioCodec);

                    ApplySpecialCodecOptions(options, selectedVideoCodec);
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