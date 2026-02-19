using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using VManager.Services.Models;
using VManager.Services.Operations;
using FFMpegCore;
using VManager.Services;
using Xunit;

namespace VManager.Tests.Integration
{
    public class CutOperationIntegrationTests : IAsyncLifetime
    {
        private const string TestFilesDir = "IntegrationTestFiles_Cut";
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
        public async Task CutVideo_ShouldProduceCorrectDuration_AndHandleWarnings()
        {
            string inputPath = Path.Combine(TestFilesDir, "cut_input.mp4");
            string outputPath = Path.Combine(TestFilesDir, "cut_output.mp4");

            // Generar video de prueba (10 segundos, 320x240)
            // Es vital usar yuv420p para que el decoder no tenga problemas
            var generateArgs = $"-f lavfi -i testsrc=duration=10:size=320x240:rate=15 -c:v libx264 -pix_fmt yuv420p -y \"{inputPath}\"";
            
            var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = generateArgs,
                CreateNoWindow = true,
                UseShellExecute = false
            });
            await proc!.WaitForExitAsync();

            // Ejecutar Corte: De los 10s totales, cortamos desde el segundo 3 con una duración de 4s
            var operation = new CutOperation(_ffmpegPath);
            var start = TimeSpan.FromSeconds(3);
            var duration = TimeSpan.FromSeconds(4);

            var result = await operation.ExecuteAsync(
                inputPath,
                outputPath,
                start,
                duration,
                new Progress<IFFmpegProcessor.ProgressInfo>(),
                CancellationToken.None
            );

            // Validaciones
            result.Success.Should().BeTrue($"Error en la operación: {result.Message}");
            File.Exists(outputPath).Should().BeTrue("El archivo de salida debería haberse creado.");

            // Analizamos el archivo resultante con FFProbe
            var mediaInfo = await FFProbe.AnalyseAsync(outputPath);
            
            // La duración debe estar cerca de los 4 segundos.
            // Nota: Al usar '-c copy', FFmpeg corta en el Keyframe más cercano, 
            // por lo que permitimos un pequeño margen de error.
            mediaInfo.Duration.TotalSeconds.Should().BeInRange(3.5, 4.5, "La duración del corte debe ser aproximadamente 4 segundos.");
        }

        [Fact]
        public async Task CutVideo_WithExceededDuration_ShouldAdjustAutomatically()
        {
            // Test para tu lógica de: if (start + duration > totalDuration)
            string inputPath = Path.Combine(TestFilesDir, "cut_input_adjusted.mp4");
            string outputPath = Path.Combine(TestFilesDir, "cut_adjusted.mp4");

            // Generar video de prueba de 10 segundos
            var generateArgs = $"-f lavfi -i testsrc=duration=10:size=320x240:rate=15 -c:v libx264 -pix_fmt yuv420p -y \"{inputPath}\"";
            
            var proc = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = generateArgs,
                CreateNoWindow = true,
                UseShellExecute = false
            });
            await proc!.WaitForExitAsync();

            var operation = new CutOperation(_ffmpegPath);
            var start = TimeSpan.FromSeconds(8);
            var duration = TimeSpan.FromSeconds(5); // 8 + 5 = 13 (se pasa de los 10s originales)

            var result = await operation.ExecuteAsync(
                inputPath,
                outputPath,
                start,
                duration,
                new Progress<IFFmpegProcessor.ProgressInfo>(),
                CancellationToken.None
            );

            result.Success.Should().BeTrue();
            result.Message.Should().Contain("ajustó", "Debería avisar que se ajustó la duración al final del video.");
            
            var mediaInfo = await FFProbe.AnalyseAsync(outputPath);
            mediaInfo.Duration.TotalSeconds.Should().BeInRange(1.5, 2.5, "Debería haber cortado solo los 2 segundos restantes.");
        }
    }
}