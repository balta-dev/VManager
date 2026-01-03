// Services/Operations/AudioExtractOperation.cs
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFMpegCore;
using VManager.Services.Core;
using VManager.Services.Models;
using VManager.Services.Core.Execution;
using VManager.Services.Core.Media;

namespace VManager.Services.Operations
{
    internal class AudioExtractOperation
    {
        private readonly IFFmpegExecutor _executor;
        private readonly IMediaAnalyzer _analyzer;

        // PARA TESTS
        public AudioExtractOperation(
            IFFmpegExecutor executor,
            IMediaAnalyzer analyzer)
        {
            _executor = executor;
            _analyzer = analyzer;
        }

        // PARA PRODUCCIÓN (FFmpegProcessor)
        public AudioExtractOperation(string ffmpegPath)
        {
            _executor = new FFmpegExecutor(ffmpegPath);
            _analyzer = new MediaAnalyzer();
        }

        public async Task<ProcessingResult> ExecuteAsync(
            string inputPath,
            string outputPath,
            string? videoCodec, // no usado
            string? audioCodec, // no usado
            string selectedAudioFormat,
            IProgress<IFFmpegProcessor.ProgressInfo> progress,
            CancellationToken cancellationToken = default)
        {
            Console.WriteLine($"[DEBUG] Audiofy - Origen: {inputPath}, Destino: {outputPath}, Formato: '{selectedAudioFormat}'");

            if (string.IsNullOrEmpty(selectedAudioFormat))
                return new ProcessingResult(false, "Formato de audio no especificado.");

            var analysisResult = await _analyzer.AnalyzeAsync(inputPath);
            if (!analysisResult.Success)
                return new ProcessingResult(false, analysisResult.Message);

            var mediaInfo = analysisResult.Result!;
            if (mediaInfo.AudioStreams.Count == 0)
                return new ProcessingResult(false, "El archivo no contiene pistas de audio.");

            var originalAudioStream = mediaInfo.AudioStreams.First();
            string originalCodec = originalAudioStream.CodecName?.ToLowerInvariant() ?? "unknown";
            double duration = mediaInfo.Duration.TotalSeconds;

            var decision = AudioCodecHelper.DecideAudioProcessing(originalCodec, selectedAudioFormat);

            Console.WriteLine($"[DEBUG] Decisión: {decision.Action}, Códec: {decision.Codec}, Razón: {decision.Reason}");

            var args = FFMpegArguments
                .FromFileInput(inputPath)
                .OutputToFile(outputPath, overwrite: true, options =>
                {
                    options
                        .WithCustomArgument("-vn")           // sin video
                        .WithCustomArgument("-map 0:a");     // todas las pistas de audio

                    if (decision.Action == AudioProcessingAction.Copy)
                    {
                        options.WithAudioCodec("copy");
                    }
                    else
                    {
                        options
                            .WithAudioCodec(decision.Codec)
                            .WithAudioBitrate(decision.Bitrate);
                    }
                });

            var result = await _executor.ExecuteAsync(
                inputPath,
                outputPath,
                args,
                duration,
                progress,
                cancellationToken
            );

            if (result.Success)
            {
                string message = decision.Action == AudioProcessingAction.Copy
                    ? $"¡Audio extraído sin recodificar! ({decision.Reason})"
                    : "¡Audio extraído y convertido!";
                return new ProcessingResult(true, message, result.OutputPath);
            }

            return result;
        }
    }
}