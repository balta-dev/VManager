using Xunit;
using FluentAssertions;
using VManager.Services.Core.Media;

namespace VManager.Tests.Unit
{
    public class OutputPathBuilderTests
    {
        // ─────────────────────────────────────────────
        // COMPRESS
        // ─────────────────────────────────────────────

        [Fact]
        public void GetCompressOutputPath_AddsPercentageSuffix()
        {
            var result = OutputPathBuilder.GetCompressOutputPath(
                @"C:\Videos\test.mp4", 75);

            result.Should().Be(@"C:\Videos\test-75.mp4");
        }

        [Fact]
        public void GetCompressOutputPath_AllowsAnyPercentage()
        {
            var result = OutputPathBuilder.GetCompressOutputPath(
                @"C:\Videos\test.mp4", -10);

            result.Should().Be(@"C:\Videos\test--10.mp4");
        }

        // ─────────────────────────────────────────────
        // CONVERT
        // ─────────────────────────────────────────────

        [Fact]
        public void GetConvertOutputPath_AddsConvertSuffixAndFormat()
        {
            var result = OutputPathBuilder.GetConvertOutputPath(
                @"C:\Videos\test.mkv", "webm");

            result.Should().Be(@"C:\Videos\test-VCONV.webm");
        }

        [Fact]
        public void GetConvertOutputPath_FileWithMultipleDots_PreservesName()
        {
            var result = OutputPathBuilder.GetConvertOutputPath(
                @"C:\Videos\my.video.final.mkv", "mp4");

            result.Should().Be(@"C:\Videos\my.video.final-VCONV.mp4");
        }

        // ─────────────────────────────────────────────
        // AUDIO
        // ─────────────────────────────────────────────

        [Theory]
        [InlineData("mp3")]
        [InlineData(".mp3")]
        public void GetAudioOutputPath_NormalizesAudioFormat(string format)
        {
            var result = OutputPathBuilder.GetAudioOutputPath(
                @"C:\Videos\test.mkv", format);

            result.Should().Be(@"C:\Videos\test-ACONV.mp3");
        }

        // ─────────────────────────────────────────────
        // CUT
        // ─────────────────────────────────────────────

        [Fact]
        public void GetCutOutputPath_AppendsCutSuffix()
        {
            var result = OutputPathBuilder.GetCutOutputPath(
                @"C:\Videos\test.mp4");

            result.Should().Be(@"C:\Videos\test-VCUT.mp4");
        }

        // ─────────────────────────────────────────────
        // TEMP
        // ─────────────────────────────────────────────

        [Fact]
        public void GetTempDirectory_UsesConfiguredTempFolderName()
        {
            var inputPath = Path.Combine("Videos", "test.mp4");

            var result = OutputPathBuilder.GetTempDirectory(inputPath);

            result.Should().Be(
                Path.Combine("Videos", "vmanager_temp"));
        }
    }
}
