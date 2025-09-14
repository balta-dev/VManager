using System;
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
            IProgress<double> progress);

        Task<ProcessingResult> CompressAsync(
            string inputPath,
            string outputPath,
            int compressionPercentage,
            string videoCodec,
            string audioCodec,
            IProgress<double> progress);
    }
    //public record ProcessingResult(bool Success, string Message, string? OutputFile = null);
}