using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using VManager.Services;
using VManager.Services.Operations;
using VManager.Services.Models;
using Xunit;

namespace VManager.Tests.Integration
{
    public class AudioExtractOperationIntegrationTests : IAsyncLifetime
    {
        private const string TestFilesDir = "AudioTestFiles";
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

        private async Task CreateTestVideoWithAudio(string path)
        {
            // Genera un video de 2s con una onda senoidal de audio (aac)
            var args = $"-f lavfi -i testsrc=duration=2:size=320x240:rate=15 -f lavfi -i sine=frequency=1000:duration=2 -c:v libx264 -c:a aac -pix_fmt yuv420p -y \"{path}\"";
            
            var proc = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    Arguments = args,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            proc.Start();
            await proc.WaitForExitAsync();
            
            if (proc.ExitCode != 0)
                throw new Exception("No se pudo crear el video de prueba con audio.");
        }

        private async Task CreateTestVideoWithoutAudio(string path)
        {
            // Genera un video de 2s SIN audio
            var args = $"-f lavfi -i testsrc=duration=2:size=320x240:rate=15 -c:v libx264 -pix_fmt yuv420p -y \"{path}\"";
            
            var proc = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    Arguments = args,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            proc.Start();
            await proc.WaitForExitAsync();
        }

        [Fact]
        public async Task ExtractAudio_ToMp3_ShouldSucceed()
        {
            // Arrange
            string inputPath = Path.Combine(TestFilesDir, "video_con_audio.mp4");
            string outputPath = Path.Combine(TestFilesDir, "extracted.mp3");
            await CreateTestVideoWithAudio(inputPath);

            var operation = new AudioExtractOperation(_ffmpegPath);
            var progress = new Progress<IFFmpegProcessor.ProgressInfo>();

            // Act
            var result = await operation.ExecuteAsync(
                inputPath,
                outputPath,
                videoCodec: null,
                audioCodec: null,
                selectedAudioFormat: "mp3",
                progress: progress
            );

            // Assert
            result.Success.Should().BeTrue($"La extracción falló: {result.Message}");
            File.Exists(outputPath).Should().BeTrue();
            new FileInfo(outputPath).Length.Should().BeGreaterThan(0);
            result.Message.Should().ContainAny("¡Audio extraído", "convertido");
        }

        [Fact]
        public async Task ExtractAudio_FromVideoWithoutAudio_ShouldReturnError()
        {
            // Arrange
            string inputPath = Path.Combine(TestFilesDir, "video_sin_audio.mp4");
            string outputPath = Path.Combine(TestFilesDir, "fallara.mp3");
            await CreateTestVideoWithoutAudio(inputPath);

            var operation = new AudioExtractOperation(_ffmpegPath);

            // Act
            var result = await operation.ExecuteAsync(
                inputPath,
                outputPath,
                videoCodec: null,
                audioCodec: null,
                selectedAudioFormat: "mp3",
                progress: new Progress<IFFmpegProcessor.ProgressInfo>()
            );

            // Assert
            result.Success.Should().BeFalse();
            result.Message.Should().Contain("no contiene pistas de audio");
        }

        [Fact]
        public async Task ExtractAudio_WhenFormatNotSpecified_ShouldReturnError()
        {
            // Arrange
            string inputPath = Path.Combine(TestFilesDir, "test.mp4");
            // No necesitamos crear el archivo porque la validación de formato es lo primero
            var operation = new AudioExtractOperation(_ffmpegPath);

            // Act
            var result = await operation.ExecuteAsync(
                inputPath,
                "output.mp3",
                null, null,
                selectedAudioFormat: "", // Vacío
                progress: new Progress<IFFmpegProcessor.ProgressInfo>()
            );

            // Assert
            result.Success.Should().BeFalse();
            result.Message.Should().Be("Formato de audio no especificado.");
        }
    }
}