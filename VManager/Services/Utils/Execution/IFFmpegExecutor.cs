using System;
using System.Threading;
using System.Threading.Tasks;
using FFMpegCore;
using VManager.Services.Models;

namespace VManager.Services.Utils.Execution
{
    internal interface IFFmpegExecutor
    {
        Task<ProcessingResult> ExecuteAsync(
            string inputPath,
            string outputPath,
            FFMpegArgumentProcessor args,
            double duration,
            IProgress<IFFmpegProcessor.ProgressInfo> progress,
            CancellationToken cancellationToken);
    }
}