using System;
using System.Threading;
using System.Threading.Tasks;
using FFMpegCore;
using VManager.Services;
using VManager.Services.Models;
using VManager.Services.Utils.Execution;

namespace VManager.Tests.Fakes
{
    internal class FakeResumableFFmpegExecutor : IResumableFFmpegExecutor
    {
        public Task<ProcessingResult> ExecuteResumableAsync(
            string inputPath,
            string outputPath,
            Func<FFMpegArgumentOptions, FFMpegArgumentOptions> optionsBuilder,
            double duration,
            IProgress<IFFmpegProcessor.ProgressInfo> progress,
            CancellationToken cancellationToken,
            string? operationName = null)
        {
            return Task.FromResult(new ProcessingResult(true, "ok resumable", outputPath));
        }
    }
}