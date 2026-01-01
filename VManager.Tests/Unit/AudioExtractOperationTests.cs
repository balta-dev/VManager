using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FFMpegCore;
using FFMpegCore.Builders.MetaData;
using FluentAssertions;
using VManager.Services.Models;
using VManager.Services.Operations;
using VManager.Tests.Fakes;
using Xunit;

namespace VManager.Tests.Unit
{
    public class AudioExtractOperationTests
    {
        [Fact]
        public async Task ExecuteAsync_WhenAnalyzerFails_ReturnsError()
        {
            // Arrange
            var executor = new FakeFFmpegExecutor();

            // Creamos un IMediaAnalysis válido con AudioStreams mínimos
            var fakeAnalysis = new FakeMediaAnalysis()
            {
                Duration = TimeSpan.FromSeconds(10),
                AudioStreams = new List<AudioStream> { new AudioStream { CodecName = "aac" } },
                VideoStreams = new List<VideoStream>(),
                SubtitleStreams = new List<SubtitleStream>(),
                Chapters = new List<ChapterData>(),
                Format = new MediaFormat()
            };

            var analyzer = new FakeMediaAnalyzer
            {
                Result = new AnalysisResult<IMediaAnalysis>(
                    success: false,
                    message: "analyzer failed",
                    result: fakeAnalysis
                )
            };

            var operation = new AudioExtractOperation(executor, analyzer);

            // Act
            var result = await operation.ExecuteAsync(
                inputPath: "input.mp4",
                outputPath: "out.mp3",
                videoCodec: null,
                audioCodec: null,
                selectedAudioFormat: "mp3",
                progress: null!,
                cancellationToken: CancellationToken.None
            );

            // Assert
            result.Success.Should().BeFalse();
            result.Message.Should().Be("analyzer failed");
        }
    }
}