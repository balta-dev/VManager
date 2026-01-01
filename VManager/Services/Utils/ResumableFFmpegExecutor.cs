using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFMpegCore;
using VManager.Services.Models;
using VManager.Services.Utils;

namespace VManager.Services;

internal class ResumableFFmpegExecutor
        {
            private readonly string _ffmpegPath;
            private const int ChunkDurationSeconds = 60; // 60 segundos por chunk (ajustable)

            public ResumableFFmpegExecutor(string ffmpegPath)
            {
                _ffmpegPath = ffmpegPath;
            }

            public async Task<ProcessingResult> ExecuteResumableAsync(
                string inputPath,
                string outputPath,
                Func<FFMpegArgumentOptions, FFMpegArgumentOptions> configureOptions, // función para configurar códecs, bitrate, etc.
                double totalDuration,
                IProgress<IVideoProcessor.ProgressInfo> progress,
                CancellationToken ct,
                string operationName = "unknown")
            {
                string tempFolder = ResumeManager.GetTempFolder(inputPath);
                Directory.CreateDirectory(tempFolder);

                string chunkFolder = Path.Combine(tempFolder, "chunks");
                string processedFolder = Path.Combine(tempFolder, "processed");
                Directory.CreateDirectory(chunkFolder);
                Directory.CreateDirectory(processedFolder);

                // Cargar progreso previo
                var progressLog = ResumeManager.LoadProgress(inputPath);
                var completedChunks = progressLog?.CompletedChunks ?? new List<int>();

                // Calcular cantidad de chunks
                int totalChunks = (int)Math.Ceiling(totalDuration / ChunkDurationSeconds);

                // 1. Dividir en chunks (solo si no existen)
                if (!Directory.EnumerateFiles(chunkFolder).Any())
                {
                    var splitResult = await SplitIntoChunks(inputPath, chunkFolder, ct);
                    if (!splitResult.Success) return splitResult;
                }

                // 2. Procesar chunks pendientes
                double processedDuration = 0;
                for (int i = 0; i < totalChunks; i++)
                {
                    if (ct.IsCancellationRequested) break;

                    int chunkIndex = i + 1;
                    if (completedChunks.Contains(chunkIndex))
                    {
                        processedDuration += ChunkDurationSeconds;
                        continue;
                    }

                    string chunkInput = Path.Combine(chunkFolder, $"{ProcessingConstants.ChunkPrefix}{chunkIndex:D03}.mp4");
                    string chunkOutput = Path.Combine(processedFolder, $"{ProcessingConstants.ProcessedChunkPrefix}{chunkIndex:D03}.mp4");

                    var args = FFMpegArguments
                        .FromFileInput(chunkInput)
                        .OutputToFile(chunkOutput, overwrite: true, options => configureOptions(options));

                    var chunkResult = await new FFmpegExecutor(_ffmpegPath).ExecuteAsync(
                        chunkInput,
                        chunkOutput,
                        args,
                        Math.Min(ChunkDurationSeconds, totalDuration - processedDuration),
                        new Progress<IVideoProcessor.ProgressInfo>(p =>
                        {
                            double globalProgress = (processedDuration + p.Progress * ChunkDurationSeconds) / totalDuration;
                            double remainingSeconds = (totalDuration - processedDuration - p.Progress * ChunkDurationSeconds) / Math.Max(p.Progress, 0.01);
                            TimeSpan remaining = TimeSpan.FromSeconds(remainingSeconds);

                            progress.Report(new IVideoProcessor.ProgressInfo(globalProgress, remaining));
                        }),
                        ct
                    );

                    if (!chunkResult.Success)
                    {
                        ResumeManager.ClearProgress(inputPath);
                        return chunkResult;
                    }

                    completedChunks.Add(chunkIndex);
                    processedDuration += ChunkDurationSeconds;

                    // Guardar progreso
                    ResumeManager.SaveProgress(inputPath, new ResumeProgress
                    {
                        CompletedChunks = completedChunks,
                        TotalDurationProcessed = processedDuration,
                        Operation = operationName
                    });
                }

                // 3. Concatenar
                var concatResult = await ConcatenateChunks(processedFolder, totalChunks, outputPath, ct);
                if (!concatResult.Success) return concatResult;

                // 4. Limpieza
                ResumeManager.ClearProgress(inputPath);

                return new ProcessingResult(true, "¡Operación completada (con recuperación)!", outputPath);
            }

            private async Task<ProcessingResult> SplitIntoChunks(string inputPath, string chunkFolder, CancellationToken ct)
            {
                string pattern = Path.Combine(chunkFolder, $"{ProcessingConstants.ChunkPrefix}%03d.mp4");

                var args = FFMpegArguments
                    .FromFileInput(inputPath)
                    .OutputToFile(pattern, overwrite: true, options =>
                    {
                        options
                            .WithCustomArgument("-f segment")
                            .WithCustomArgument($"-segment_time {ChunkDurationSeconds}")
                            .WithCustomArgument("-c copy")
                            .WithCustomArgument("-reset_timestamps 1")
                            .WithCustomArgument("-avoid_negative_ts make_zero");
                    });

                return await new FFmpegExecutor(_ffmpegPath).ExecuteAsync(
                    inputPath,
                    pattern,
                    args,
                    0,
                    new Progress<IVideoProcessor.ProgressInfo>(_ => { }),
                    ct
                );
            }

            private async Task<ProcessingResult> ConcatenateChunks(string processedFolder, int totalChunks, string finalOutput, CancellationToken ct)
            {
                // Carpeta padre de "processed" (donde está vmanager_temp)
                string tempFolder = Path.GetDirectoryName(processedFolder)!;
                string listPath = Path.Combine(tempFolder, ProcessingConstants.ConcatListFile);

                // Crear la lista de archivos
                var lines = new List<string>();
                for (int i = 1; i <= totalChunks; i++)
                {
                    lines.Add($"file '{ProcessingConstants.ProcessedChunkPrefix}{i:D03}.mp4'");
                }

                await File.WriteAllLinesAsync(listPath, lines, ct);

                // Todos los argumentos personalizados van con WithCustomArgument
                var args = FFMpegArguments
                    .FromFileInput(listPath)
                    .OutputToFile(finalOutput, overwrite: true, options =>
                    {
                        options
                            .WithCustomArgument("-f concat")   // ← Con WithCustomArgument
                            .WithCustomArgument("-safe 0")     // ← Con WithCustomArgument
                            .WithCustomArgument("-c copy");    // ← Con WithCustomArgument
                    });

                return await new FFmpegExecutor(_ffmpegPath).ExecuteAsync(
                    listPath,
                    finalOutput,
                    args,
                    0,
                    new Progress<IVideoProcessor.ProgressInfo>(_ => { }),
                    ct
                );
            }
        }