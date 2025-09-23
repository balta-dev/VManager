using System;
using System.Threading;
using System.Threading.Tasks;
namespace VManager.Services
{
    public interface IVideoProcessor
    {
        Task<ProcessingResult> CutAsync(
            string inputPath,
            string outputPath,
            TimeSpan start,
            TimeSpan duration,
            IProgress<double> progress,
            CancellationToken cancellationToken = default);

        Task<ProcessingResult> CompressAsync(
            string inputPath,
            string outputPath,
            int compressionPercentage,
            string videoCodec,
            string audioCodec,
            IProgress<double> progress,
            CancellationToken cancellationToken = default);

        Task<ProcessingResult> ConvertAsync(
            string inputPath,
            string outputPath,
            string? videoCodec,
            string? audioCodec,
            string selectedFormat,
            IProgress<double> progress,
            CancellationToken cancellationToken = default);
    }
    //public record ProcessingResult(bool Success, string Message, string? OutputFile = null);
}