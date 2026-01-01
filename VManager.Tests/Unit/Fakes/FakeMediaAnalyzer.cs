using System.Threading.Tasks;
using FFMpegCore;
using VManager.Services.Models;
using VManager.Services.Utils.Media;

namespace VManager.Tests.Fakes
{
    internal class FakeMediaAnalyzer : IMediaAnalyzer
    {
        public AnalysisResult<IMediaAnalysis>? Result { get; set; }

        public Task<AnalysisResult<IMediaAnalysis>> AnalyzeAsync(string inputPath)
            => Task.FromResult(Result!);
    }
}