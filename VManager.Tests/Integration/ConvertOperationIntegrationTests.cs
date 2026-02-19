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
    public class ConvertOperationIntegrationTests : IAsyncLifetime
    {
        private const string TestFilesDir = "IntegrationTestFiles";
        private string _ffmpegPath = string.Empty;

        public async Task InitializeAsync()
        {
            await FFmpegManager.Initialize();
            _ffmpegPath = FFmpegManager.FfmpegPath;

            // Limpieza inicial
            if (Directory.Exists(TestFilesDir))
                Directory.Delete(TestFilesDir, true);
            
            Directory.CreateDirectory(TestFilesDir);
        }

        public Task DisposeAsync()
        {
            // Limpieza al finalizar (opcional)
            // if (Directory.Exists(TestFilesDir)) Directory.Delete(TestFilesDir, true);
            return Task.CompletedTask;
        }

        [Fact]
        public async Task ConvertShortVideo_ProducesOutputFile()
        {
            string inputPath = Path.Combine(TestFilesDir, "test_input.mp4");
            string outputPath = Path.Combine(TestFilesDir, "test_output.mp4");

            // Generar el video de entrada CORRECTAMENTE
            // Usamos yuv420p porque es el estándar que aceptan TODOS los perfiles (Baseline, Main, High)
            var generateArgs = $"-f lavfi -i testsrc=duration=2:size=320x240:rate=15 -c:v libx264 -pix_fmt yuv420p -y \"{inputPath}\"";
    
            var proc = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _ffmpegPath,
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

            // Ejecutar la operación de conversión
            var operation = new ConvertOperation(_ffmpegPath);
            var result = await operation.ExecuteAsync(
                inputPath,
                outputPath,
                videoCodec: "libx264",
                audioCodec: "aac",
                selectedFormat: "mp4",
                progress: new Progress<IFFmpegProcessor.ProgressInfo>(),
                cancellationToken: CancellationToken.None
            );

            // Validar
            result.Success.Should().BeTrue($"Error de FFmpeg: {result.Message}");
            File.Exists(outputPath).Should().BeTrue();
        }
        
        [Fact]
        public async Task ConvertLongVideo_ShouldTriggerResumableExecutor()
        {
            string inputPath = Path.Combine(TestFilesDir, "long_convert_input.mp4");
            string outputPath = Path.Combine(TestFilesDir, "long_convert_output.mkv"); // Cambiamos formato para testear

            // Generar video de 130 segundos (2 chunks de 60s + 1 de 10s)
            var generateArgs = $"-f lavfi -i testsrc=duration=130:size=160x120:rate=10 -c:v libx264 -pix_fmt yuv420p -y \"{inputPath}\"";
            
            var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = generateArgs,
                CreateNoWindow = true,
                UseShellExecute = false
            });
            await proc!.WaitForExitAsync();

            // Ejecutar Conversión
            var operation = new ConvertOperation(_ffmpegPath);
            
            // El progreso debería reportar varias veces ya que hay múltiples chunks
            int progressCount = 0;
            bool tempFolderWasCreated = false;
            string tempFolder = Path.Combine(Path.GetDirectoryName(inputPath)!, ".vmanager_temp_" + Path.GetFileNameWithoutExtension(inputPath));
            
            var progress = new Progress<IFFmpegProcessor.ProgressInfo>(p => {
                progressCount++;
                // Verificar si la carpeta temporal existe durante la ejecución
                if (Directory.Exists(tempFolder))
                    tempFolderWasCreated = true;
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

            // Validaciones
            result.Success.Should().BeTrue($"La conversión resumible falló: {result.Message}");
            File.Exists(outputPath).Should().BeTrue();
            
            // CRÍTICO: Verificar que se usó el ResumableExecutor
            tempFolderWasCreated.Should().BeTrue("El ResumableExecutor debería haber creado una carpeta temporal de chunks");
            
            // El ResumableExecutor debería haber reportado progreso múltiples veces (uno por chunk mínimo)
            progressCount.Should().BeGreaterThan(1, "El ResumableExecutor debería reportar progreso para cada chunk procesado");
            
            // Verificar que el archivo final realmente sea el formato pedido (MKV)
            var mediaInfo = await FFProbe.AnalyseAsync(outputPath);
            mediaInfo.Format.FormatName.Should().Contain("matroska");

            // Verificar limpieza de temporales
            Directory.Exists(tempFolder).Should().BeFalse("La carpeta temporal de chunks debería haber sido eliminada.");
        }
    }
}