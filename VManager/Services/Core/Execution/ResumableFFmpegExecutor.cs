using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FFMpegCore;
using VManager.Services.Models;

namespace VManager.Services.Core.Execution;

internal class ResumableFFmpegExecutor : IResumableFFmpegExecutor
{
    private readonly string _ffmpegPath;
    private const int ChunkDurationSeconds = 60;

    public ResumableFFmpegExecutor(string ffmpegPath) => _ffmpegPath = ffmpegPath;

    public virtual async Task<ProcessingResult> ExecuteResumableAsync(
        string inputPath,
        string outputPath,
        Func<FFMpegArgumentOptions, FFMpegArgumentOptions> configureOptions,
        double totalDuration,
        IProgress<IFFmpegProcessor.ProgressInfo> progress,
        CancellationToken ct,
        string? operationName = null)
    {
        string tempFolder = ResumeManager.GetTempFolder(inputPath);
        string chunkFolder = Path.Combine(tempFolder, "chunks");
        string processedFolder = Path.Combine(tempFolder, "processed");

        Directory.CreateDirectory(chunkFolder);
        Directory.CreateDirectory(processedFolder);

        // 1. SPLIT: Si no hay chunks originales, cortamos el video.
        if (!Directory.Exists(chunkFolder) || !Directory.EnumerateFiles(chunkFolder).Any())
        {
            var splitResult = await SplitIntoChunks(inputPath, chunkFolder, ct);
            if (!splitResult.Success) return splitResult;
        }

        // 2. CARGA DE PROGRESO:
        int totalChunks = Directory.GetFiles(chunkFolder, "vmanager_chunk*.mp4").Length;
        var progressLog = ResumeManager.LoadProgress(inputPath);
        var completedChunks = progressLog?.CompletedChunks ?? new List<int>();

        // 3. BUCLE DE PROCESAMIENTO RESILIENTE
        for (int i = 1; i <= totalChunks; i++)
        {
            if (ct.IsCancellationRequested) break;

            string chunkInput = Path.Combine(chunkFolder, $"vmanager_chunk{i:D03}.mp4");
            string chunkOutput = Path.Combine(processedFolder, $"processed_chunk{i:D03}.mp4");

            // VALIDACIÓN DE ORO: ¿Está en el JSON Y el archivo físico es válido?
            if (completedChunks.Contains(i) && ResumeManager.IsChunkValid(chunkOutput))
            {
                continue; // Saltamos este chunk, ya está bien hecho.
            }

            // Si llegamos acá, el chunk no está o está corrupto.
            // Aseguramos que no esté en la lista y que el archivo viejo (si existe) se borre.
            completedChunks.Remove(i);
            if (File.Exists(chunkOutput)) File.Delete(chunkOutput);

            if (!File.Exists(chunkInput))
                return new ProcessingResult(false, $"Error crítico: Falta el fragmento de origen {i}. Reinicie el proceso.");

            var args = FFMpegArguments.FromFileInput(chunkInput)
                .OutputToFile(chunkOutput, overwrite: true, options => configureOptions(options));

            // Llamada a tu Executor (maneja la cancelación y borrado del archivo actual)
            var result = await new FFmpegExecutor(_ffmpegPath).ExecuteAsync(
                chunkInput, chunkOutput, args, ChunkDurationSeconds, progress, ct);

            if (!result.Success) return result;

            // SOLO GUARDAMOS si terminó exitosamente
            completedChunks.Add(i);
            ResumeManager.SaveProgress(inputPath, new ResumeProgress 
            { 
                CompletedChunks = completedChunks.OrderBy(x => x).ToList() 
            });
        }

        if (ct.IsCancellationRequested)
            return new ProcessingResult(false, "Operación cancelada por el usuario.");

        // 4. CONCATENACIÓN FINAL
        var concatResult = await ConcatenateChunks(processedFolder, totalChunks, outputPath, ct);
        
        if (concatResult.Success)
            ResumeManager.ClearProgress(inputPath);

        return concatResult;
    }

    private async Task<ProcessingResult> SplitIntoChunks(string inputPath, string chunkFolder, CancellationToken ct)
    {
        string pattern = Path.Combine(chunkFolder, "vmanager_chunk%03d.mp4");
        var args = FFMpegArguments.FromFileInput(inputPath)
            .OutputToFile(pattern, true, opt => opt
                .WithCustomArgument("-f segment")
                .WithCustomArgument($"-segment_time {ChunkDurationSeconds}")
                .WithCustomArgument("-segment_start_number 1")
                .WithCustomArgument("-c copy"));

        var result = await new FFmpegExecutor(_ffmpegPath).ExecuteAsync(inputPath, pattern, args, 0, null!, ct);

        // Parche para el patrón de archivos
        if (!result.Success && Directory.EnumerateFiles(chunkFolder).Any())
            return new ProcessingResult(true, "Split OK", pattern);

        return result;
    }

    private async Task<ProcessingResult> ConcatenateChunks(string processedFolder, int totalChunks, string finalOutput, CancellationToken ct)
    {
        string tempFolder = Path.GetDirectoryName(processedFolder)!;
        string listPath = Path.Combine(tempFolder, "concat_list.txt");

        var validLines = new List<string>();
        for (int i = 1; i <= totalChunks; i++)
        {
            string chunkPath = Path.Combine(processedFolder, $"processed_chunk{i:D03}.mp4");
            if (ResumeManager.IsChunkValid(chunkPath))
                validLines.Add($"file '{Path.GetFullPath(chunkPath).Replace("\\", "/")}'");
        }

        if (validLines.Count < totalChunks)
            return new ProcessingResult(false, $"Integridad fallida: faltan {totalChunks - validLines.Count} fragmentos.");

        await File.WriteAllLinesAsync(listPath, validLines, ct);

        var args = FFMpegArguments.FromFileInput(listPath, true, opt => opt.WithCustomArgument("-f concat").WithCustomArgument("-safe 0"))
            .OutputToFile(finalOutput, true, opt => opt.WithCustomArgument("-c copy"));

        return await new FFmpegExecutor(_ffmpegPath).ExecuteAsync(listPath, finalOutput, args, 0, null!, ct);
    }
}