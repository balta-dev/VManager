using System.Threading;
using System.Threading.Tasks;
using FFMpegCore;
using VManager.Services;
using VManager.Services.Models;
using VManager.Services.Utils.Execution;

namespace VManager.Tests.Fakes
{
    internal class FakeFFmpegExecutor : IFFmpegExecutor
    {
        public Task<ProcessingResult> ExecuteAsync(
            string inputPath,
            string outputPath,
            FFMpegArgumentProcessor args,
            double duration,
            IProgress<IFFmpegProcessor.ProgressInfo> progress,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                new ProcessingResult(true, "ok", outputPath)
            );
        }
    }
}