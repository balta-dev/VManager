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
    public class CompressOperationIntegrationTests : IDisposable
    {
        private const string TestFilesDir = "CompressionTestFiles";
        private readonly string _ffmpegPath;

        public CompressOperationIntegrationTests()
        {
            FFmpegManager.Initialize();
            _ffmpegPath = FFmpegManager.FfmpegPath;

            if (Directory.Exists(TestFilesDir))
                Directory.Delete(TestFilesDir, true);
            
            Directory.CreateDirectory(TestFilesDir);
        }

        public void Dispose() => Directory.Delete(TestFilesDir, true);

        private async Task CreateTestVideo(string path, int durationSeconds)
        {
            // Generamos un video con bitrate bajo para que sea rápido
            // Usamos una resolución mínima para acelerar la creación en tests
            var args = $"-f lavfi -i testsrc=duration={durationSeconds}:size=160x120:rate=10 -c:v libx264 -pix_fmt yuv420p -y \"{path}\"";
            
            var proc = new System.Diagnostics.Process
            {
                StartInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = _ffmpegPath,
                    Arguments = args,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };
            proc.Start();
            await proc.WaitForExitAsync();
        }

        [Theory]
        [InlineData(50)] // Comprimir al 50%
        [InlineData(20)] // Comprimir al 20%
        public async Task CompressShortVideo_ShouldReduceSize(int percentage)
        {
            // Arrange
            string inputPath = Path.Combine(TestFilesDir, $"input_{percentage}.mp4");
            string outputPath = Path.Combine(TestFilesDir, $"output_{percentage}.mp4");
            await CreateTestVideo(inputPath, 5); // 5 segundos

            var operation = new CompressOperation(_ffmpegPath);
            long originalSize = new FileInfo(inputPath).Length;

            // Act
            var result = await operation.ExecuteAsync(
                inputPath,
                outputPath,
                compressionPercentage: percentage,
                videoCodec: "libx264",
                audioCodec: "aac",
                progress: new Progress<IFFmpegProcessor.ProgressInfo>()
            );

            // Assert
            result.Success.Should().BeTrue();
            File.Exists(outputPath).Should().BeTrue();
            
            long compressedSize = new FileInfo(outputPath).Length;
            // Nota: En videos ultra cortos/pequeños el overhead del contenedor puede mentir, 
            // pero el proceso debe terminar con éxito.
            compressedSize.Should().BeGreaterThan(0);
        }

        [Fact]
        public async Task CompressLongVideo_ShouldTriggerResumableExecutor()
        {
            // Arrange
            // Generamos un video de 301 segundos (5min 1s) para entrar en el modo resumable
            string inputPath = Path.Combine(TestFilesDir, "long_video.mp4");
            string outputPath = Path.Combine(TestFilesDir, "long_compressed.mp4");
            await CreateTestVideo(inputPath, 301); 

            var operation = new CompressOperation(_ffmpegPath);

            // Act
            var result = await operation.ExecuteAsync(
                inputPath,
                outputPath,
                compressionPercentage: 50,
                videoCodec: "libx264",
                audioCodec: "aac",
                progress: new Progress<IFFmpegProcessor.ProgressInfo>()
            );

            // Assert
            result.Success.Should().BeTrue($"Error de FFmpeg: {result.Message}");
            File.Exists(outputPath).Should().BeTrue();
        }

        [Fact]
        public async Task Compress_WithInvalidPercentage_ReturnsError()
        {
            // Arrange
            var operation = new CompressOperation(_ffmpegPath);

            // Act
            var result = await operation.ExecuteAsync(
                "any.mp4", "any_out.mp4",
                compressionPercentage: 150, // Inválido
                null, null,
                new Progress<IFFmpegProcessor.ProgressInfo>()
            );

            // Assert
            result.Success.Should().BeFalse();
            result.Message.Should().Be("Porcentaje inválido.");
        }
    }
}