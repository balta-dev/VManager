using System.Threading.Tasks;
using FFMpegCore;
using VManager.Services.Models;

namespace VManager.Services.Core.Media
{
    internal interface IMediaAnalyzer
    {
        Task<AnalysisResult<IMediaAnalysis>> AnalyzeAsync(string inputPath);
    }
}