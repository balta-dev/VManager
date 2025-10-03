using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using FFMpegCore;

namespace VManager.Services
{
    public class VideoProcessor : IVideoProcessor
    {
        private string _ffmpegPath = FFmpegManager.FfmpegPath;
        public static class ErrorMessages
        {
            public const string FileNotFound = "Archivo no encontrado.";
            public const string InvalidPercentage = "Porcentaje inválido.";
            public const string AnalysisError = "Error al analizar el video: {0}";
            public const string InvalidDuration = "Error al obtener duración.";
            public const string InvalidCutParameters = "Parámetros de corte inválidos.";
        }

        private (string videoCodec, string audioCodec) GetDefaultCodecs(string? videoCodec, string? audioCodec)
        {
            return (videoCodec ?? "libx264", audioCodec ?? "aac");
        }

        private static async Task<AnalysisResult<IMediaAnalysis>> AnalyzeVideoAsync(string inputPath)
        {
            try
            {
                if (!File.Exists(inputPath))
                    return new AnalysisResult<IMediaAnalysis>(false, ErrorMessages.FileNotFound);
                
                var mediaInfo = await FFProbe.AnalyseAsync(inputPath);
                double duration = mediaInfo.Duration.TotalSeconds;
                if (duration <= 0)
                {
                    return new AnalysisResult<IMediaAnalysis>(false, ErrorMessages.InvalidDuration);
                }
                return new AnalysisResult<IMediaAnalysis>(true, "Análisis completado", mediaInfo);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG]: Error: {ex.Message}");
                Console.WriteLine($"[DEBUG]: Stack Trace: {ex.StackTrace}");
                return new AnalysisResult<IMediaAnalysis>(false, string.Format(ErrorMessages.AnalysisError, ex.Message));
            }
        }
        
        public async Task<ProcessingResult> CutAsync(
        string inputPath,
        string outputPath,
        TimeSpan start,
        TimeSpan duration,
        IProgress<double> progress,
        CancellationToken cancellationToken = default)
    {
        var analysisResult = await AnalyzeVideoAsync(inputPath);
        if (!analysisResult.Success)
        {
            return new ProcessingResult(false, analysisResult.Message);
        }

        var mediaInfo = analysisResult.Result!;
        double totalDuration = mediaInfo.Duration.TotalSeconds;

        string directory = Path.GetDirectoryName(inputPath)!;
        string fileName = Path.GetFileNameWithoutExtension(inputPath);
        string extension = Path.GetExtension(inputPath);
        outputPath = Path.Combine(directory, $"{fileName}-VCUT{extension}");

        // Validar parámetros de corte
        string? warningMessage = null;
        if (start < TimeSpan.Zero || duration <= TimeSpan.Zero)
        {
            return new ProcessingResult(false, ErrorMessages.InvalidCutParameters);
        }
        if (start.TotalSeconds + duration.TotalSeconds > totalDuration)
        {
            duration = TimeSpan.FromSeconds(totalDuration - start.TotalSeconds);
            string formatted = duration.ToString(@"hh\:mm\:ss");
            warningMessage = $"Nota: La duración del corte se ajustó automáticamente a {formatted}.";
        }

        try
        {
            Console.WriteLine($"Corte - Video: copy, Audio: copy, Inicio: {start}, Duración: {duration.ToString(@"hh\:mm\:ss")}");

            var args = FFMpegArguments
                .FromFileInput(inputPath, false, options => options.Seek(start))
                .OutputToFile(outputPath, overwrite: true, options =>
                {
                    options
                        .WithVideoCodec("copy")
                        .WithAudioCodec("copy")
                        .WithDuration(duration);
                });

            // Configurar proceso manualmente (igual que CompressAsync)
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    Arguments = args.Arguments,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
            {
                // ✅ Solo una forma de cancelación (como en CompressAsync)
                cts.Token.Register(() =>
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                        Console.WriteLine("[DEBUG]: Proceso FFmpeg terminado por cancelación.");
                        if (File.Exists(outputPath))
                        {
                            try
                            {
                                File.Delete(outputPath);
                                Console.WriteLine("[DEBUG]: Archivo de salida eliminado tras cancelación.");
                            }
                            catch (IOException ex)
                            {
                                Console.WriteLine($"[DEBUG]: No se pudo eliminar el archivo inmediatamente: {ex.Message}");
                                // Se intentará eliminar después de WaitForExitAsync
                            }
                        }
                    }
                });

                process.Start();

                // ✅ Lectura simple y directa (como en CompressAsync)
                using (var reader = process.StandardError)
                {
                    string? line;
                    while ((line = await reader.ReadLineAsync()) != null)
                    {
                        if (line.Contains("time="))
                        {
                            // ✅ Regex corregido (sin escapes dobles)
                            var timeMatch = Regex.Match(line, @"time=(\d{2}:\d{2}:\d{2}\.\d{2})");
                            if (timeMatch.Success && TimeSpan.TryParse(timeMatch.Groups[1].Value, out var time))
                            {
                                progress.Report(Math.Min(time.TotalSeconds / duration.TotalSeconds, 1.0));
                            }
                        }
                    }
                }

                // ✅ Wait simple (como en CompressAsync)
                await process.WaitForExitAsync(cts.Token);

                if (cts.Token.IsCancellationRequested)
                {
                    if (File.Exists(outputPath))
                    {
                        File.Delete(outputPath);
                        Console.WriteLine("[DEBUG]: Archivo de salida eliminado tras cancelación.");
                    }
                    Console.WriteLine("[DEBUG]: Corte cancelado por el usuario.");
                    return new ProcessingResult(false, "Corte cancelado por el usuario.");
                }

                if (process.ExitCode != 0 && !cts.Token.IsCancellationRequested)
                {
                    string? errorOutput = await process.StandardError.ReadToEndAsync();
                    if (errorOutput.Contains("No such file or directory") ||
                        errorOutput.Contains("Permission denied") ||
                        errorOutput.Contains("Could not create") ||
                        errorOutput.Contains("Invalid argument"))
                    {
                        throw new Exception($"FFmpeg error: {errorOutput} (ExitCode: {process.ExitCode})");
                    }
                }
            }

            if (!string.IsNullOrEmpty(warningMessage))
            {
                return new ProcessingResult(true, "¡Corte finalizado!", outputPath, warningMessage);
            }

            return new ProcessingResult(true, "¡Corte finalizado!", outputPath);
        }
        catch (OperationCanceledException)
        {
            if (File.Exists(outputPath))
            {
                File.Delete(outputPath);
                Console.WriteLine("[DEBUG]: Archivo de salida eliminado tras cancelación.");
            }
            Console.WriteLine("[DEBUG]: Corte cancelado por el usuario.");
            return new ProcessingResult(false, "Corte cancelado por el usuario.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DEBUG]: Error: {ex.Message}");
            return new ProcessingResult(false, $"Error: {ex.Message}");
        }
    }


        public async Task<ProcessingResult> CompressAsync(
        string inputPath,
        string outputPath,
        int compressionPercentage,
        string? videoCodec,
        string? audioCodec,
        IProgress<double> progress,
        CancellationToken cancellationToken = default)
        {
            var (selectedVideoCodec, selectedAudioCodec) = GetDefaultCodecs(videoCodec, audioCodec);

            var analysisResult = await AnalyzeVideoAsync(inputPath);
            if (!analysisResult.Success)
            {
                return new ProcessingResult(false, analysisResult.Message);
            }

            string extension = Path.GetExtension(inputPath);
            string outputFileName = Path.GetFileNameWithoutExtension(inputPath) + $"-{compressionPercentage}{extension}";
            outputPath = Path.Combine(Path.GetDirectoryName(inputPath)!, outputFileName);

            var mediaInfo = analysisResult.Result!;
            double duration = mediaInfo.Duration.TotalSeconds;

            long sizeBytes = new FileInfo(inputPath).Length;
            long targetSize = sizeBytes * compressionPercentage / 100;
            int targetBitrate = (int)((targetSize * 8) / duration / 1000);
            targetBitrate = Math.Max(targetBitrate, 100);

            try
            {
                Console.WriteLine($"Compresión - Video: {selectedVideoCodec}, Audio: {selectedAudioCodec}, Bitrate: {targetBitrate} kbps");

                var args = FFMpegArguments
                    .FromFileInput(inputPath)
                    .OutputToFile(outputPath, overwrite: true, options =>
                    {
                        options
                            .WithVideoCodec(selectedVideoCodec)
                            .WithVideoBitrate(targetBitrate)
                            .WithAudioCodec(selectedAudioCodec)
                            .WithAudioBitrate(128);
                        ConfigureHardwareAcceleration(options, selectedVideoCodec);
                    });

                // Configurar el proceso manualmente
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _ffmpegPath,
                        Arguments = args.Arguments,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    cts.Token.Register(() =>
                    {
                        if (!process.HasExited)
                        {
                            process.Kill();
                            Console.WriteLine("[DEBUG]: Proceso FFmpeg terminado por cancelación.");
                            if (File.Exists(outputPath))
                            {
                                File.Delete(outputPath);
                                Console.WriteLine("[DEBUG]: Archivo de salida eliminado tras cancelación.");
                            }
                        }
                    });

                    process.Start();

                    using (var reader = process.StandardError)
                    {
                        string? line;
                        while ((line = await reader.ReadLineAsync()) != null)
                        {
                            if (line.Contains("time="))
                            {
                                var timeMatch = Regex.Match(line, @"time=(\d{2}:\d{2}:\d{2}\.\d{2})");
                                if (timeMatch.Success && TimeSpan.TryParse(timeMatch.Groups[1].Value, out var time))
                                {
                                    progress.Report(time.TotalSeconds / duration);
                                }
                            }
                        }
                    }

                    await process.WaitForExitAsync(cts.Token);

                    if (cts.Token.IsCancellationRequested)
                    {
                        if (File.Exists(outputPath))
                        {
                            File.Delete(outputPath);
                            Console.WriteLine("[DEBUG]: Archivo de salida eliminado tras cancelación.");
                        }
                        Console.WriteLine("[DEBUG]: Compresión cancelada por el usuario.");
                        return new ProcessingResult(false, "Compresión cancelada por el usuario.");
                    }

                    if (process.ExitCode != 0 && !cts.Token.IsCancellationRequested)
                    {
                        string? errorOutput = await process.StandardError.ReadToEndAsync();
                        if (errorOutput.Contains("No such file or directory") ||
                            errorOutput.Contains("Permission denied") ||
                            errorOutput.Contains("Could not create") ||
                            errorOutput.Contains("Invalid argument"))
                        {
                            throw new Exception($"FFmpeg error: {errorOutput} (ExitCode: {process.ExitCode})");
                        }
                    }
                }

                return new ProcessingResult(true, "¡Compresión finalizada!", outputPath);
            }
            catch (OperationCanceledException)
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                    Console.WriteLine("[DEBUG]: Archivo de salida eliminado tras cancelación.");
                }
                Console.WriteLine("[DEBUG]: Compresión cancelada por el usuario.");
                return new ProcessingResult(false, "Compresión cancelada por el usuario.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG]: Error: {ex.Message}");
                return new ProcessingResult(false, $"Error: {ex.Message}");
            }
        }
        
        public async Task<ProcessingResult> ConvertAsync(
            string inputPath,
            string outputPath,
            string? videoCodec,
            string? audioCodec,
            string selectedFormat,
            IProgress<double> progress,
            CancellationToken cancellationToken = default)
        {
            var (selectedVideoCodec, selectedAudioCodec) = GetDefaultCodecs(videoCodec, audioCodec);

            var analysisResult = await AnalyzeVideoAsync(inputPath);
            if (!analysisResult.Success)
            {
                return new ProcessingResult(false, analysisResult.Message);
            }

            string outputFileName = Path.GetFileNameWithoutExtension(inputPath) + $"-VCONV.{selectedFormat}";
            outputPath = Path.Combine(Path.GetDirectoryName(inputPath)!, outputFileName);

            var mediaInfo = analysisResult.Result!;
            double duration = mediaInfo.Duration.TotalSeconds;

            try
            {
                Console.WriteLine($"Conversión - Video: {selectedVideoCodec}, Audio: {selectedAudioCodec}, Extension: {selectedFormat}");

                var args = FFMpegArguments
                    .FromFileInput(inputPath)
                    .OutputToFile(outputPath, overwrite: true, options =>
                    {
                        options
                            .WithVideoCodec(selectedVideoCodec)
                            .WithAudioCodec(selectedAudioCodec)
                            .WithAudioBitrate(128);
                        ConfigureHardwareAcceleration(options, selectedVideoCodec);
                    });

                // Configurar el proceso manualmente
                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _ffmpegPath,
                        Arguments = args.Arguments, // Obtener los argumentos generados
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    // Registrar la acción de cancelación
                    cts.Token.Register(() =>
                    {
                        if (!process.HasExited)
                        {
                            process.Kill();
                            Console.WriteLine("[DEBUG]: Proceso FFmpeg terminado por cancelación.");
                            if (File.Exists(outputPath))
                            {
                                File.Delete(outputPath);
                                Console.WriteLine("[DEBUG]: Archivo de salida eliminado tras cancelación.");
                            }
                        }
                    });

                    process.Start();

                    // Leer el progreso desde stderr
                    using (var reader = process.StandardError)
                    {
                        string? line;
                        while ((line = await reader.ReadLineAsync()) != null)
                        {
                            if (line.Contains("time="))
                            {
                                var timeMatch = Regex.Match(line, @"time=(\d{2}:\d{2}:\d{2}\.\d{2})");
                                if (timeMatch.Success && TimeSpan.TryParse(timeMatch.Groups[1].Value, out var time))
                                {
                                    progress.Report(time.TotalSeconds / duration);
                                }
                            }
                        }
                    }

                    await process.WaitForExitAsync(cts.Token);

                    if (cts.Token.IsCancellationRequested)
                    {
                        //entra acá
                        if (File.Exists(outputPath))
                        {
                            File.Delete(outputPath);
                            Console.WriteLine("[DEBUG]: Archivo de salida eliminado tras cancelación.");
                        }
                        Console.WriteLine("[DEBUG]: Conversión cancelada por el usuario.");
                        return new ProcessingResult(false, "Conversión cancelada por el usuario.");
                    }
                    
                    if (process.ExitCode != 0 && !cts.Token.IsCancellationRequested)
                    {
                        string? errorOutput = await process.StandardError.ReadToEndAsync();
                        if (errorOutput.Contains("No such file or directory") ||
                            errorOutput.Contains("Permission denied") ||
                            errorOutput.Contains("Could not create") ||
                            errorOutput.Contains("Invalid argument"))
                        {
                            throw new Exception($"FFmpeg error: {errorOutput} (ExitCode: {process.ExitCode})");
                        }
                    }
                }

                return new ProcessingResult(true, "¡Conversión finalizada!", outputPath);
            }
            catch (OperationCanceledException)
            {
                if (File.Exists(outputPath))
                {
                    File.Delete(outputPath);
                    Console.WriteLine("[DEBUG]: Archivo de salida eliminado tras cancelación.");
                }
                Console.WriteLine("[DEBUG]: Conversión cancelada por el usuario.");
                return new ProcessingResult(false, "Conversión cancelada por el usuario.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG]: Error: {ex.Message}");
                return new ProcessingResult(false, $"Error: {ex.Message}");
            }
        }
        
        public async Task<ProcessingResult> AudiofyAsync(
            string inputPath,
            string outputPath,
            string? videoCodec,
            string? audioCodec,
            string selectedAudioFormat,
            IProgress<double> progress,
            CancellationToken cancellationToken = default)
        {
            Console.WriteLine($"[DEBUG] AudiofyAsync - inputPath: {inputPath}");
            Console.WriteLine($"[DEBUG] AudiofyAsync - selectedAudioFormat: '{selectedAudioFormat}'");
            
            if (string.IsNullOrEmpty(selectedAudioFormat))
            {
                return new ProcessingResult(false, "Formato de audio no especificado.");
            }

            // Analizar archivo de entrada
            var analysisResult = await AnalyzeVideoAsync(inputPath);
            if (!analysisResult.Success)
            {
                return new ProcessingResult(false, analysisResult.Message);
            }

            var mediaInfo = analysisResult.Result!;
            
            // Verificar que tenga pistas de audio
            if (mediaInfo.AudioStreams.Count == 0)
            {
                return new ProcessingResult(false, "El archivo no contiene pistas de audio.");
            }

            // Obtener información del audio original
            var originalAudioStream = mediaInfo.AudioStreams.First();
            string originalCodec = originalAudioStream.CodecName?.ToLower() ?? "unknown";
            
            Console.WriteLine($"[DEBUG] Códec de audio original: {originalCodec}");
            Console.WriteLine($"[DEBUG] Formato de salida deseado: {selectedAudioFormat}");

            string outputFileName = Path.GetFileNameWithoutExtension(inputPath) + $"-ACONV.{selectedAudioFormat}";
            outputPath = Path.Combine(Path.GetDirectoryName(inputPath)!, outputFileName);

            double duration = mediaInfo.Duration.TotalSeconds;

            // Decidir si necesitamos recodificar o solo cambiar contenedor
            var processingDecision = DecideAudioProcessing(originalCodec, selectedAudioFormat);
            
            // Usar el formato normalizado para el nombre del archivo
            string normalizedFormat = NormalizeFormatName(selectedAudioFormat);
            string correctedOutputFileName = Path.GetFileNameWithoutExtension(inputPath) + $"-ACONV.{normalizedFormat}";
            outputPath = Path.Combine(Path.GetDirectoryName(inputPath)!, correctedOutputFileName);
            
            Console.WriteLine($"[DEBUG] Decisión: {processingDecision.Action}");
            Console.WriteLine($"[DEBUG] Códec a usar: {processingDecision.Codec}");
            Console.WriteLine($"[DEBUG] Razón: {processingDecision.Reason}");
            Console.WriteLine($"[DEBUG] Archivo de salida corregido: {outputPath}");

            try
            {
                var args = FFMpegArguments
                    .FromFileInput(inputPath)
                    .OutputToFile(outputPath, overwrite: true, options =>
                    {
                        options.WithCustomArgument("-vn"); // Sin video
                        
                        if (processingDecision.Action == AudioProcessingAction.Copy)
                        {
                            // Solo copiar el audio sin recodificar
                            options.WithAudioCodec("copy");
                        }
                        else
                        {
                            // Recodificar con el códec especificado
                            options
                                .WithAudioCodec(processingDecision.Codec)
                                .WithAudioBitrate(processingDecision.Bitrate);
                        }
                    });

                Console.WriteLine($"[DEBUG] Argumentos FFmpeg: {args.Arguments}");

                var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = _ffmpegPath,
                        Arguments = args.Arguments,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                {
                    cts.Token.Register(() =>
                    {
                        if (!process.HasExited)
                        {
                            process.Kill();
                            Console.WriteLine("[DEBUG]: Proceso FFmpeg terminado por cancelación.");
                            if (File.Exists(outputPath))
                            {
                                try { File.Delete(outputPath); }
                                catch (IOException ex) { Console.WriteLine($"[DEBUG]: {ex.Message}"); }
                            }
                        }
                    });

                    process.Start();

                    using (var reader = process.StandardError)
                    {
                        string? line;
                        while ((line = await reader.ReadLineAsync()) != null)
                        {
                            if (line.Contains("time="))
                            {
                                var timeMatch = Regex.Match(line, @"time=(\d{2}:\d{2}:\d{2}\.\d{2})");
                                if (timeMatch.Success && TimeSpan.TryParse(timeMatch.Groups[1].Value, out var time))
                                {
                                    progress.Report(Math.Min(time.TotalSeconds / duration, 1.0));
                                }
                            }
                        }
                    }

                    await process.WaitForExitAsync(cts.Token);

                    if (cts.Token.IsCancellationRequested)
                    {
                        if (File.Exists(outputPath)) File.Delete(outputPath);
                        return new ProcessingResult(false, "Extracción cancelada por el usuario.");
                    }

                    if (process.ExitCode != 0)
                    {
                        return new ProcessingResult(false, $"FFmpeg falló con código: {process.ExitCode}");
                    }
                }

                if (!File.Exists(outputPath) || new FileInfo(outputPath).Length == 0)
                {
                    if (File.Exists(outputPath)) File.Delete(outputPath);
                    return new ProcessingResult(false, "El archivo de salida no se creó correctamente.");
                }

                string successMessage = processingDecision.Action == AudioProcessingAction.Copy 
                    ? $"¡Audio extraído sin recodificar! ({processingDecision.Reason})"
                    : "¡Audio extraído y convertido!";
                    
                return new ProcessingResult(true, successMessage, outputPath);
            }
            catch (OperationCanceledException)
            {
                if (File.Exists(outputPath)) File.Delete(outputPath);
                return new ProcessingResult(false, "Extracción cancelada por el usuario.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DEBUG]: Error: {ex.Message}");
                return new ProcessingResult(false, $"Error: {ex.Message}");
            }
        }

        // Estructura para la decisión de procesamiento
        private struct AudioProcessingDecision
        {
            public AudioProcessingAction Action;
            public string Codec;
            public int Bitrate;
            public string Reason;
        }

        private enum AudioProcessingAction
        {
            Copy,      // Solo cambiar contenedor, mantener códec
            Reencode   // Recodificar audio
        }

        // Método que decide si recodificar o no
        private AudioProcessingDecision DecideAudioProcessing(string originalCodec, string targetFormat)
        {
            var decision = new AudioProcessingDecision();
            
            // Normalizar el formato de entrada (puede venir como códec en lugar de formato)
            string normalizedFormat = NormalizeFormatName(targetFormat);
            
            // Mapeos de códecs que pueden ir directamente a ciertos formatos
            var compatibleMappings = new Dictionary<string, HashSet<string>>
            {
                ["aac"] = new HashSet<string> { "aac", "m4a", "mp4" },
                ["mp3"] = new HashSet<string> { "mp3" },
                ["flac"] = new HashSet<string> { "flac" },
                ["vorbis"] = new HashSet<string> { "ogg", "oga" },
                ["opus"] = new HashSet<string> { "opus", "ogg" },
                ["pcm_s16le"] = new HashSet<string> { "wav" },
                ["pcm_s24le"] = new HashSet<string> { "wav" },
                ["pcm_f32le"] = new HashSet<string> { "wav" }
            };

            // Verificar si el códec original es compatible con el formato de salida
            if (compatibleMappings.ContainsKey(originalCodec) && 
                compatibleMappings[originalCodec].Contains(normalizedFormat))
            {
                decision.Action = AudioProcessingAction.Copy;
                decision.Codec = "copy";
                decision.Bitrate = 0; // No aplica
                decision.Reason = $"Códec {originalCodec} compatible con formato {normalizedFormat}";
                return decision;
            }

            // Si no es compatible, necesitamos recodificar
            decision.Action = AudioProcessingAction.Reencode;
            decision.Reason = $"Códec {originalCodec} no compatible con {normalizedFormat}, recodificando";

            // Elegir códec y bitrate apropiados para el formato de salida
            switch (normalizedFormat)
            {
                case "mp3":
                    decision.Codec = "libmp3lame";
                    decision.Bitrate = 192;
                    break;
                case "aac":
                case "m4a":
                    decision.Codec = "aac";
                    decision.Bitrate = 128;
                    break;
                case "ogg":
                case "oga":
                    decision.Codec = "libvorbis";
                    decision.Bitrate = 192;
                    break;
                case "flac":
                    decision.Codec = "flac";
                    decision.Bitrate = 0; // Lossless
                    break;
                case "wav":
                    decision.Codec = "pcm_s16le";
                    decision.Bitrate = 0; // Sin compresión
                    break;
                case "opus":
                    decision.Codec = "libopus";
                    decision.Bitrate = 128;
                    break;
                case "wma":
                    decision.Codec = "wmav2";
                    decision.Bitrate = 128;
                    break;
                default:
                    // Fallback a AAC
                    decision.Codec = "aac";
                    decision.Bitrate = 128;
                    break;
            }

            return decision;
        }

        // Método para normalizar nombres de códecs a formatos
        private string NormalizeFormatName(string input)
        {
            var codecToFormat = new Dictionary<string, string>
            {
                ["libmp3lame"] = "mp3",
                ["libvorbis"] = "ogg",
                ["libopus"] = "opus",
                ["pcm_s16le"] = "wav",
                ["pcm_s24le"] = "wav",
                ["pcm_f32le"] = "wav",
                ["wmav2"] = "wma"
            };
            
            string normalized = input.ToLower();
            return codecToFormat.ContainsKey(normalized) ? codecToFormat[normalized] : normalized;
        }


        private void ConfigureHardwareAcceleration(FFMpegArgumentOptions opts, string videoCodec)
        {
            var codec = videoCodec.ToLower();

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                ConfigureWindowsAcceleration(opts, codec);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                ConfigureLinuxAcceleration(opts, codec);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                ConfigureMacAcceleration(opts, codec);
            }
        }

        private void ConfigureWindowsAcceleration(FFMpegArgumentOptions opts, string codec)
        {
            switch (codec)
            {
                // NVIDIA NVENC
                case "h264_nvenc":
                case "hevc_nvenc":
                case "av1_nvenc":
                    opts.WithCustomArgument("-preset fast")
                        .WithCustomArgument("-profile:v main")
                        .WithCustomArgument("-rc vbr")
                        .WithCustomArgument("-gpu 0");
                    break;

                // AMD AMF
                case "h264_amf":
                case "hevc_amf":
                case "av1_amf":
                    opts.WithCustomArgument("-usage transcoding")
                        .WithCustomArgument("-profile:v main")
                        .WithCustomArgument("-quality speed")
                        .WithCustomArgument("-rc vbr_peak");
                    break;

                // Intel QuickSync
                case "h264_qsv":
                case "hevc_qsv":
                case "av1_qsv":
                    opts.WithCustomArgument("-preset fast")
                        .WithCustomArgument("-profile:v main")
                        .WithCustomArgument("-look_ahead 0");
                    break;

                // Windows Media Foundation
                case "h264_mf":
                case "hevc_mf":
                    break;

                // Software codecs - configuración optimizada
                case "libx264":
                    opts.WithCustomArgument("-preset fast")
                        .WithCustomArgument("-profile:v main");
                    break;

                case "libx265":
                    opts.WithCustomArgument("-preset fast")
                        .WithCustomArgument("-profile:v main");
                    break;
            }
        }

        private void ConfigureLinuxAcceleration(FFMpegArgumentOptions opts, string codec)
        {
            switch (codec)
            {
                // VAAPI (AMD/Intel)
                case "h264_vaapi":
                case "hevc_vaapi":
                case "vp8_vaapi":
                case "vp9_vaapi":
                case "av1_vaapi":
                    opts.WithCustomArgument("-vaapi_device /dev/dri/renderD128")
                        .WithCustomArgument("-vf format=nv12,hwupload");

                    if (codec == "h264_vaapi" || codec == "hevc_vaapi")
                    {
                        opts.WithCustomArgument("-profile:v main");
                    }
                    break;

                // NVIDIA NVENC en Linux
                case "h264_nvenc":
                case "hevc_nvenc":
                case "av1_nvenc":
                    opts.WithCustomArgument("-preset fast")
                        .WithCustomArgument("-profile:v main")
                        .WithCustomArgument("-rc vbr")
                        .WithCustomArgument("-gpu 0");
                    break;

                // V4L2 Memory-to-Memory (Raspberry Pi, ARM, etc.)
                case "h264_v4l2m2m":
                case "vp8_v4l2m2m":
                    opts.WithCustomArgument("-pix_fmt yuv420p");
                    break;

                // Vulkan
                case "h264_vulkan":
                case "hevc_vulkan":
                    opts.WithCustomArgument("-vulkan_params device=0");
                    break;

                // Software codecs optimizados
                case "libx264":
                    opts.WithCustomArgument("-preset fast")
                        .WithCustomArgument("-profile:v main");
                    break;

                case "libx265":
                    opts.WithCustomArgument("-preset fast")
                        .WithCustomArgument("-profile:v main");
                    break;
            }
        }

        private void ConfigureMacAcceleration(FFMpegArgumentOptions opts, string codec)
        {
            switch (codec)
            {
                // VideoToolbox
                case "h264_videotoolbox":
                case "hevc_videotoolbox":
                    opts.WithCustomArgument("-profile:v main")
                        .WithCustomArgument("-pix_fmt yuv420p")
                        .WithCustomArgument("-realtime 0");
                    break;

                case "prores_videotoolbox":
                    opts.WithCustomArgument("-profile:v 2"); // ProRes 422
                    break;

                // Software codecs
                case "libx264":
                    opts.WithCustomArgument("-preset fast")
                        .WithCustomArgument("-profile:v main");
                    break;

                case "libx265":
                    opts.WithCustomArgument("-preset fast")
                        .WithCustomArgument("-profile:v main");
                    break;
            }
        }
    }

    public class ProcessingResult
    {
        public bool Success { get; }
        public string Message { get; }
        public string OutputPath { get; }
        public string Warning { get; }

        public ProcessingResult(bool success, string message, string outputPath = null, string warning = null)
        {
            Success = success;
            Message = message;
            OutputPath = outputPath;
            Warning = warning;
        }
    }

    public class AnalysisResult<T>
    {
        public bool Success { get; }
        public string Message { get; }
        public T? Result { get; }

        public AnalysisResult(bool success, string message, T? result = default)
        {
            Success = success;
            Message = message;
            Result = result;
        }
    }
}