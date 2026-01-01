using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using VManager.Services;
using VManager.Services.Operations;
using VManager.Services.Models;
using FFMpegCore;
using Xunit;

namespace VManager.Tests.Integration
{
    public class ConvertOperationIntegrationTests
    {
        private const string TestFilesDir = "IntegrationTestFiles";

        [Fact]
        public async Task ConvertShortVideo_ProducesOutputFile()
        {
            FFmpegManager.Initialize();
            var ffmpegPath = FFmpegManager.FfmpegPath;
    
            // 1. Limpieza total para evitar archivos corruptos de ejecuciones previas
            if (Directory.Exists(TestFilesDir)) Directory.Delete(TestFilesDir, true);
            Directory.CreateDirectory(TestFilesDir);

            string inputPath = Path.Combine(TestFilesDir, "test_input.mp4");
            string outputPath = Path.Combine(TestFilesDir, "test_output.mp4");

            // 2. Generar el video de entrada CORRECTAMENTE
            // Usamos yuv420p porque es el estándar que aceptan TODOS los perfiles (Baseline, Main, High)
            var generateArgs = $"-f lavfi -i testsrc=duration=2:size=320x240:rate=15 -c:v libx264 -pix_fmt yuv420p -y \"{inputPath}\"";
    
            var proc = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = ffmpegPath,
                    Arguments = generateArgs,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
    
            proc.Start();
            await proc.WaitForExitAsync();
    
            if (proc.ExitCode != 0) 
                throw new Exception($"Fallo al crear video de prueba: {await proc.StandardError.ReadToEndAsync()}");

            // 3. Ejecutar la operación de conversión
            var operation = new ConvertOperation(ffmpegPath);
            var result = await operation.ExecuteAsync(
                inputPath,
                outputPath,
                videoCodec: "libx264",
                audioCodec: "aac",
                selectedFormat: "mp4",
                progress: new Progress<IFFmpegProcessor.ProgressInfo>(),
                cancellationToken: CancellationToken.None
            );

            // 4. Validar
            result.Success.Should().BeTrue($"Error de FFmpeg: {result.Message}");
            File.Exists(outputPath).Should().BeTrue();
        }
        
        [Fact]
        public async Task ConvertLongVideo_ShouldTriggerResumableExecutor()
        {
            FFmpegManager.Initialize();
            var ffmpegPath = FFmpegManager.FfmpegPath;

            // 1. Limpieza
            if (Directory.Exists(TestFilesDir)) Directory.Delete(TestFilesDir, true);
            Directory.CreateDirectory(TestFilesDir);

            string inputPath = Path.Combine(TestFilesDir, "long_convert_input.mp4");
            string outputPath = Path.Combine(TestFilesDir, "long_convert_output.mkv"); // Cambiamos formato para testear

            // 2. Generar video de 130 segundos (2 chunks de 60s + 1 de 10s)
            var generateArgs = $"-f lavfi -i testsrc=duration=130:size=160x120:rate=10 -c:v libx264 -pix_fmt yuv420p -y \"{inputPath}\"";
            
            var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = generateArgs,
                CreateNoWindow = true,
                UseShellExecute = false
            });
            await proc!.WaitForExitAsync();

            // 3. Ejecutar Conversión
            var operation = new ConvertOperation(ffmpegPath);
            
            // El progreso debería reportar varias veces ya que hay múltiples chunks
            int progressCount = 0;
            var progress = new Progress<IFFmpegProcessor.ProgressInfo>(p => {
                progressCount++;
            });

            var result = await operation.ExecuteAsync(
                inputPath,
                outputPath,
                videoCodec: "libx264",
                audioCodec: "aac",
                selectedFormat: "mkv",
                progress: progress,
                cancellationToken: CancellationToken.None
            );

            // 4. Validaciones
            result.Success.Should().BeTrue($"La conversión resumible falló: {result.Message}");
            File.Exists(outputPath).Should().BeTrue();
            
            // Verificar que el archivo final realmente sea el formato pedido (MKV)
            var mediaInfo = await FFProbe.AnalyseAsync(outputPath);
            mediaInfo.Format.FormatName.Should().Contain("matroska");

            // Verificar limpieza de temporales
            string tempFolder = Path.Combine(Path.GetDirectoryName(inputPath)!, ".vmanager_temp_" + Path.GetFileNameWithoutExtension(inputPath));
            Directory.Exists(tempFolder).Should().BeFalse("La carpeta temporal de chunks debería haber sido eliminada.");
        }
    }
}