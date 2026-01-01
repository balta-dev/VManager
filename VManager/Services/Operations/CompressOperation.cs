// Services/Operations/CompressOperation.cs
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FFMpegCore;
using VManager.Services;
using VManager.Services.Models;

namespace VManager.Services.Operations
{
    internal class CompressOperation
    {
        private readonly FFmpegExecutor _executor;
        private readonly ResumableFFmpegExecutor _resumableExecutor;
        private readonly MediaAnalyzer _analyzer;

        public CompressOperation(string ffmpegPath)
        {
            _executor = new FFmpegExecutor(ffmpegPath);
            _resumableExecutor = new ResumableFFmpegExecutor(ffmpegPath);
            _analyzer = new MediaAnalyzer();
        }

        public async Task<ProcessingResult> ExecuteAsync(
            string inputPath,
            string outputPath,
            int compressionPercentage,
            string? videoCodec,
            string? audioCodec,
            IProgress<IVideoProcessor.ProgressInfo> progress,
            CancellationToken cancellationToken = default)
        {
            if (compressionPercentage <= 0 || compressionPercentage > 100)
                return new ProcessingResult(false, "Porcentaje inválido.");

            var (selectedVideoCodec, selectedAudioCodec) = (videoCodec ?? "libx264", audioCodec ?? "aac");

            var analysisResult = await _analyzer.AnalyzeAsync(inputPath);
            if (!analysisResult.Success)
                return new ProcessingResult(false, analysisResult.Message);

            var mediaInfo = analysisResult.Result!;
            double duration = mediaInfo.Duration.TotalSeconds;

            long sizeBytes = new FileInfo(inputPath).Length;
            long targetSize = sizeBytes * compressionPercentage / 100;
            int targetBitrate = (int)((targetSize * 8) / duration / 1000);
            targetBitrate = Math.Max(targetBitrate, 100);

            Console.WriteLine($"Compresión - Video: {selectedVideoCodec}, Audio: {selectedAudioCodec}, Bitrate: {targetBitrate} kbps");

            if (duration > 300) // >5 minutos → resumable
            {
                return await _resumableExecutor.ExecuteResumableAsync(
                    inputPath,
                    outputPath,
                    options =>
                    {
                        options
                            .WithCustomArgument("-map 0")
                            .WithVideoCodec(selectedVideoCodec)
                            .WithVideoBitrate(targetBitrate)
                            .WithAudioCodec(selectedAudioCodec)
                            .WithAudioBitrate(128);
                        HardwareAccelerationConfigurator.Configure(options, selectedVideoCodec);
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
                        .WithVideoBitrate(targetBitrate)
                        .WithAudioCodec(selectedAudioCodec)
                        .WithAudioBitrate(128);
                    HardwareAccelerationConfigurator.Configure(options, selectedVideoCodec);
                });

            return await _executor.ExecuteAsync(inputPath, outputPath, args, duration, progress, cancellationToken);
        }
    }
}