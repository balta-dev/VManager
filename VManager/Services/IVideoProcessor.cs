using System;
using System.Threading;
using System.Threading.Tasks;
using FFMpegCore;
using VManager.Services.Models;

namespace VManager.Services
{
    public interface IVideoProcessor
    {
        public record ProgressInfo(double Progress, TimeSpan Remaining);

        Task<AnalysisResult<IMediaAnalysis>> AnalyzeVideoAsync(string inputPath);

        Task<ProcessingResult> CutAsync(string inputPath, string outputPath, TimeSpan start, TimeSpan duration,
            IProgress<IVideoProcessor.ProgressInfo> progress, CancellationToken ct = default);

        Task<ProcessingResult> CompressAsync(
            string inputPath,
            string outputPath,
            int compressionPercentage,
            string videoCodec,
            string audioCodec,
            IProgress<ProgressInfo> progress,
            CancellationToken cancellationToken = default);

        Task<ProcessingResult> ConvertAsync(
            string inputPath,
            string outputPath,
            string? videoCodec,
            string? audioCodec,
            string selectedFormat,
            IProgress<ProgressInfo> progress,
            CancellationToken cancellationToken = default);
    }
    //public record ProcessingResult(bool Success, string Message, string? OutputFile = null);
}