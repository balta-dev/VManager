using System;
using System.IO;
using System.Threading.Tasks;
using FFMpegCore;
using FFMpegCore.Enums;

namespace VManager.Services
{
    public class VideoProcessor : IVideoProcessor
    {
        public async Task<ProcessingResult> CutAsync(
            string inputPath,
            string outputPath,
            TimeSpan start,
            TimeSpan duration,
            string videoCodec,
            string audioCodec,
            IProgress<double> progress)
        {
            if (!File.Exists(inputPath))
                return new ProcessingResult(false, "Archivo no encontrado.");

            var mediaInfo = await FFProbe.AnalyseAsync(inputPath);
            if (mediaInfo.Duration.TotalSeconds <= 0)
                return new ProcessingResult(false, "Error al obtener duración.");

            try
            {
                var args = FFMpegArguments
                    .FromFileInput(inputPath)
                    .OutputToFile(outputPath, overwrite: true, options => options
                        .WithVideoCodec(videoCodec)
                        .WithAudioCodec(audioCodec)
                        .Seek(start)
                        .WithDuration(duration))
                    .NotifyOnProgress(time =>
                    {
                        progress.Report(time.TotalSeconds / duration.TotalSeconds);
                    });

                await args.ProcessAsynchronously();
                return new ProcessingResult(true, "Corte realizado correctamente.", outputPath);
            }
            catch (Exception ex)
            {
                return new ProcessingResult(false, $"Error: {ex.Message}");
            }
        }

        public async Task<ProcessingResult> CompressAsync(
            string inputPath,
            string outputPath,
            int compressionPercentage,
            string videoCodec,
            string audioCodec,
            IProgress<double> progress)
        {
            if (!File.Exists(inputPath))
                return new ProcessingResult(false, "Archivo no encontrado.");

            var mediaInfo = await FFProbe.AnalyseAsync(inputPath);
            double duration = mediaInfo.Duration.TotalSeconds;
            if (duration <= 0)
                return new ProcessingResult(false, "Error al obtener duración.");

            long sizeBytes = new FileInfo(inputPath).Length;
            long targetSize = sizeBytes * compressionPercentage / 100;
            int targetBitrate = (int)((targetSize * 8) / duration / 1000);

            try
            {
                var args = FFMpegArguments
                    .FromFileInput(inputPath)
                    .OutputToFile(outputPath, overwrite: true, options => options
                        .WithVideoCodec(videoCodec)
                        .WithVideoBitrate(targetBitrate)
                        .WithAudioCodec(audioCodec)
                        .WithAudioBitrate(128))
                    .NotifyOnProgress(time =>
                    {
                        progress.Report(time.TotalSeconds / duration);
                    });

                await args.ProcessAsynchronously();
                return new ProcessingResult(true, "Compresión realizada correctamente.", outputPath);
            }
            catch (Exception ex)
            {
                return new ProcessingResult(false, $"Error: {ex.Message}");
            }
        }
    }

}
