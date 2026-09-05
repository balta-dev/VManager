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
            bool isUrl = Uri.TryCreate(inputPath, UriKind.Absolute, out var uriResult)
                && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps || uriResult.Scheme == "rtsp" || uriResult.Scheme == "rtmp");

            bool isHls = HlsPlaylistHelper.IsHlsPlaylist(inputPath);

            try
            {
                if (!isUrl && !File.Exists(inputPath))
                    return new AnalysisResult<IMediaAnalysis>(false, ErrorMessages.FileNotFound);

                IMediaAnalysis mediaInfo = isHls
                    ? await FFProbe.AnalyseAsync(inputPath, customArguments: HlsPlaylistHelper.FfprobeCustomArguments)
                    : await FFProbe.AnalyseAsync(inputPath);
                
                // Si es un stream, una duración de 0 o menor puede ser normal. Para archivos locales sí es inválida.
                // HLS VOD suele reportar duración; HLS live puede ser 0.
                if (!isUrl && !isHls && mediaInfo.Duration.TotalSeconds <= 0)
                    return new AnalysisResult<IMediaAnalysis>(false, ErrorMessages.InvalidDuration);

                if (isHls)
                    Console.WriteLine($"[HLS] Playlist detectado. Duración: {mediaInfo.Duration.TotalSeconds:F1}s");

                return new AnalysisResult<IMediaAnalysis>(true, "Análisis completado", mediaInfo);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al analizar video {(isUrl || isHls ? "(Streaming/HLS)" : "(Local)")}. {ex.Message}");
                
                // No mostrar diálogo UI molesto si es un error de streaming/red / HLS remoto
                if (!isUrl && !isHls)
                {
                    ErrorService.Show(ex);
                }
                
                return new AnalysisResult<IMediaAnalysis>(false, string.Format(ErrorMessages.AnalysisError, ex.Message));
            }
        }
    }
}
