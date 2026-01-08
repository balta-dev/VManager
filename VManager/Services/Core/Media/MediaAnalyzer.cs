using System;
using System.IO;
using System.Threading.Tasks;
using FFMpegCore;
using VManager.Services.Models;
using VManager.Services.Core;

namespace VManager.Services.Core.Media
{
    internal class MediaAnalyzer : IMediaAnalyzer
    {
        public virtual async Task<AnalysisResult<IMediaAnalysis>> AnalyzeAsync(string inputPath)
        {
            try
            {
                if (!File.Exists(inputPath))
                    return new AnalysisResult<IMediaAnalysis>(false, ErrorMessages.FileNotFound);
                
                var mediaInfo = await FFProbe.AnalyseAsync(inputPath);
                if (mediaInfo.Duration.TotalSeconds <= 0)
                    return new AnalysisResult<IMediaAnalysis>(false, ErrorMessages.InvalidDuration);

                return new AnalysisResult<IMediaAnalysis>(true, "Análisis completado", mediaInfo);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al analizar video. {ex.Message}");
                return new AnalysisResult<IMediaAnalysis>(false, string.Format(ErrorMessages.AnalysisError, ex.Message));
            }
        }
    }
}