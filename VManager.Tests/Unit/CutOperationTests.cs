using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FFMpegCore;
using FluentAssertions;
using VManager.Services.Models;
using VManager.Services.Operations;
using VManager.Tests.Fakes;
using Xunit;

namespace VManager.Tests.Unit
{
    public class CutOperationTests
    {
        [Fact]
        public async Task ExecuteAsync_WhenParametersAreValid_PerformsCut()
        {
            // Arrange
            var executor = new FakeFFmpegExecutor();
            var fakeAnalysis = new FakeMediaAnalysis
            {
                Duration = TimeSpan.FromSeconds(60) // Video de 1 minuto
            };
            var analyzer = new FakeMediaAnalyzer
            {
                Result = new AnalysisResult<IMediaAnalysis>(
                    success: true,
                    message: "ok",
                    result: fakeAnalysis
                )
            };

            var operation = new CutOperation("fake/path");
            // Reemplazamos los internos para test
            typeof(CutOperation)
                .GetField("_executor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .SetValue(operation, executor);
            typeof(CutOperation)
                .GetField("_analyzer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .SetValue(operation, analyzer);

            TimeSpan start = TimeSpan.FromSeconds(10);
            TimeSpan duration = TimeSpan.FromSeconds(20);

            // Act
            var result = await operation.ExecuteAsync(
                inputPath: "input.mp4",
                outputPath: "output.mp4",
                start: start,
                duration: duration,
                progress: null!,
                cancellationToken: CancellationToken.None
            );

            // Assert
            result.Success.Should().BeTrue();
            result.OutputPath.Should().Be("output.mp4");
        }

        [Fact]
        public async Task ExecuteAsync_WhenStartExceedsDuration_AdjustsDurationAndReturnsWarning()
        {
            // Arrange
            var executor = new FakeFFmpegExecutor();
            var fakeAnalysis = new FakeMediaAnalysis
            {
                Duration = TimeSpan.FromSeconds(30) // Video de 30 segundos
            };
            var analyzer = new FakeMediaAnalyzer
            {
                Result = new AnalysisResult<IMediaAnalysis>(
                    success: true,
                    message: "ok",
                    result: fakeAnalysis
                )
            };

            var operation = new CutOperation("fake/path");
            typeof(CutOperation)
                .GetField("_executor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .SetValue(operation, executor);
            typeof(CutOperation)
                .GetField("_analyzer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .SetValue(operation, analyzer);

            TimeSpan start = TimeSpan.FromSeconds(20);
            TimeSpan duration = TimeSpan.FromSeconds(20); // excede el final

            // Act
            var result = await operation.ExecuteAsync(
                inputPath: "input.mp4",
                outputPath: "output.mp4",
                start: start,
                duration: duration,
                progress: null!,
                cancellationToken: CancellationToken.None
            );

            // Assert
            result.Success.Should().BeTrue();
            result.OutputPath.Should().Be("output.mp4");
            result.Message.Should().Contain("Nota: La duración del corte se ajustó automáticamente");
        }

        [Fact]
        public async Task ExecuteAsync_WhenInvalidParameters_ReturnsError()
        {
            // Arrange
            var executor = new FakeFFmpegExecutor();
            var fakeAnalysis = new FakeMediaAnalysis
            {
                Duration = TimeSpan.FromSeconds(30)
            };
            var analyzer = new FakeMediaAnalyzer
            {
                Result = new AnalysisResult<IMediaAnalysis>(
                    success: true,
                    message: "ok",
                    result: fakeAnalysis
                )
            };

            var operation = new CutOperation("fake/path");
            typeof(CutOperation)
                .GetField("_executor", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .SetValue(operation, executor);
            typeof(CutOperation)
                .GetField("_analyzer", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                .SetValue(operation, analyzer);

            TimeSpan start = TimeSpan.FromSeconds(-5); // inválido
            TimeSpan duration = TimeSpan.FromSeconds(10);

            // Act
            var result = await operation.ExecuteAsync(
                inputPath: "input.mp4",
                outputPath: "output.mp4",
                start: start,
                duration: duration,
                progress: null!,
                cancellationToken: CancellationToken.None
            );

            // Assert
            result.Success.Should().BeFalse();
            result.Message.Should().Be("Parámetros de corte inválidos.");
        }
    }
}
