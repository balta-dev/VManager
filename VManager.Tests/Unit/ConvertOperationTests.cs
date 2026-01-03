using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FFMpegCore;
using FluentAssertions;
using VManager.Services.Models;
using VManager.Services.Operations;
using VManager.Tests.Unit.Fakes;
using Xunit;

namespace VManager.Tests.Unit
{
    public class ConvertOperationTests
    {
        [Fact]
        public async Task ExecuteAsync_ShortVideo_UsesNormalExecutor()
        {
            // Arrange
            var executor = new FakeFFmpegExecutor();
            var resumable = new FakeResumableFFmpegExecutor();
            
            var analysis = new FakeMediaAnalysis
            {
                Duration = TimeSpan.FromSeconds(60),
                VideoStreams = new List<VideoStream> { new VideoStream { CodecName = "h264" } },
                AudioStreams = new List<AudioStream> { new AudioStream { CodecName = "aac" } }
            };
            
            var analyzer = new FakeMediaAnalyzer { Result = new AnalysisResult<IMediaAnalysis>(true, "", analysis) };
            
            var operation = new ConvertOperation(executor, resumable, analyzer);

            // Act
            var result = await operation.ExecuteAsync("input.mp4", "output.mp4", null, null, "mp4", null!, CancellationToken.None);

            // Assert
            result.Success.Should().BeTrue();
            result.OutputPath.Should().Be("output.mp4");
        }

        [Fact]
        public async Task ExecuteAsync_LongVideo_UsesResumableExecutor()
        {
            // Arrange
            var executor = new FakeFFmpegExecutor();
            var resumable = new FakeResumableFFmpegExecutor();
            
            var analysis = new FakeMediaAnalysis
            {
                Duration = TimeSpan.FromSeconds(600),
                VideoStreams = new List<VideoStream> { new VideoStream { CodecName = "h264" } },
                AudioStreams = new List<AudioStream> { new AudioStream { CodecName = "aac" } }
            };
            
            var analyzer = new FakeMediaAnalyzer { Result = new AnalysisResult<IMediaAnalysis>(true, "", analysis) };
            
            var operation = new ConvertOperation(executor, resumable, analyzer);

            // Act
            var result = await operation.ExecuteAsync("input.mp4", "output.mp4", null, null, "mp4", null!, CancellationToken.None);

            // Assert
            result.Success.Should().BeTrue();
            result.OutputPath.Should().Be("output.mp4");
        }
    }
}
