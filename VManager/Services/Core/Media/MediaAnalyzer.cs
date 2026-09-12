using System;
using System.Diagnostics;
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

                // Duración inválida en archivo local: intentar reparar y reanalizar
                if (!isUrl && !isHls && mediaInfo.Duration.TotalSeconds <= 0)
                {
                    Console.WriteLine("[MediaAnalyzer] Duración inválida detectada. Intentando reparar el archivo...");

                    string? repairedPath = await TryRepairContainerAsync(inputPath);

                    if (repairedPath != null)
                    {
                        try
                        {
                            var repairedInfo = await FFProbe.AnalyseAsync(repairedPath);

                            if (repairedInfo.Duration.TotalSeconds > 0)
                            {
                                Console.WriteLine($"[MediaAnalyzer] Reparación exitosa. Duración recuperada: {repairedInfo.Duration}");

                                // Reemplazar el archivo original por el reparado
                                ReplaceOriginalWithRepaired(inputPath, repairedPath);

                                return new AnalysisResult<IMediaAnalysis>(true, "Análisis completado (archivo reparado)", repairedInfo);
                            }
                            else
                            {
                                Console.WriteLine("[MediaAnalyzer] La reparación no resolvió la duración.");
                            }
                        }
                        finally
                        {
                            // Limpiar el temporal si no se usó (ej: si falló y no se reemplazó)
                            if (File.Exists(repairedPath) && repairedPath != inputPath)
                            {
                                try { File.Delete(repairedPath); } catch { /* ignorar */ }
                            }
                        }
                    }

                    // Si llegamos acá, ni el original ni la reparación tienen duración válida.
                    // Igual permitimos continuar si hay streams legibles (comportamiento tipo mpv).
                    if (mediaInfo.PrimaryVideoStream == null && mediaInfo.PrimaryAudioStream == null)
                        return new AnalysisResult<IMediaAnalysis>(false, ErrorMessages.InvalidDuration);

                    Console.WriteLine("[MediaAnalyzer] Sin duración pero con streams válidos. Continuando de todos modos.");
                }

                if (isHls)
                    Console.WriteLine($"[HLS] Playlist detectado. Duración: {mediaInfo.Duration.TotalSeconds:F1}s");

                return new AnalysisResult<IMediaAnalysis>(true, "Análisis completado", mediaInfo);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al analizar video {(isUrl || isHls ? "(Streaming/HLS)" : "(Local)")}. {ex.Message}");

                if (!isUrl && !isHls)
                {
                    ErrorService.Show(ex);
                }

                return new AnalysisResult<IMediaAnalysis>(false, string.Format(ErrorMessages.AnalysisError, ex.Message));
            }
        }

        /// <summary>
        /// Intenta remuxear el archivo a un temporal para reconstruir el índice/duración,
        /// sin recodificar (rápido). Devuelve la ruta del temporal si tuvo éxito, o null si falló.
        /// </summary>
        private async Task<string?> TryRepairContainerAsync(string inputPath)
        {
            string tempPath = Path.Combine(
                Path.GetTempPath(),
                $"vmanager_repair_{Guid.NewGuid():N}{Path.GetExtension(inputPath)}");

            var psi = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-fflags +genpts -i \"{inputPath}\" -map 0 -c copy \"{tempPath}\"",
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            try
            {
                using var process = Process.Start(psi)!;
                string stderr = await process.StandardError.ReadToEndAsync();
                await process.WaitForExitAsync();

                if (process.ExitCode != 0 || !File.Exists(tempPath) || new FileInfo(tempPath).Length == 0)
                {
                    Console.WriteLine($"[MediaAnalyzer] Falló la reparación (exit {process.ExitCode}): {stderr}");
                    if (File.Exists(tempPath))
                        try { File.Delete(tempPath); } catch { /* ignorar */ }
                    return null;
                }

                return tempPath;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MediaAnalyzer] Excepción al intentar reparar: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Reemplaza el archivo original por la versión reparada (mismo path final),
        /// dejando un backup del original por si algo sale mal.
        /// </summary>
        private void ReplaceOriginalWithRepaired(string originalPath, string repairedPath)
        {
            try
            {
                string backupPath = originalPath + ".bak";

                // Evitar pisar un backup previo
                if (File.Exists(backupPath))
                    File.Delete(backupPath);

                File.Move(originalPath, backupPath);
                File.Move(repairedPath, originalPath);

                Console.WriteLine($"[MediaAnalyzer] Original respaldado en: {backupPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MediaAnalyzer] No se pudo reemplazar el original: {ex.Message}. Se mantiene el temporal en: {repairedPath}");
            }
        }
    }
}