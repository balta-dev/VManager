using FluentAssertions;
using VManager.Services.Core.Media;
using Xunit;

namespace VManager.Tests.Unit
{
    public class CodecMatchHelperTests
    {
        [Theory]
        [InlineData("hevc", "libx265")]
        [InlineData("h265", "libx265")]
        [InlineData("h264", "libx264")]
        [InlineData("avc", "libx264")]
        [InlineData("vp9", "libvpx-vp9")]
        public void FindBestVideoEncoder_PrefersSoftwareEncoderForCopy(string streamCodec, string expected)
        {
            var available = new[] { "libx264", "libx265", "hevc_nvenc", "h264_nvenc", "libvpx-vp9", "libsvtav1" };

            CodecMatchHelper.FindBestVideoEncoder(streamCodec, available)
                .Should().Be(expected);
        }

        [Theory]
        [InlineData("aac", "aac")]
        [InlineData("mp3", "libmp3lame")]
        [InlineData("opus", "libopus")]
        public void FindBestAudioEncoder_MapsCommonCodecs(string streamCodec, string expected)
        {
            var available = new[] { "aac", "libmp3lame", "libopus", "flac", "pcm_s24le" };

            CodecMatchHelper.FindBestAudioEncoder(streamCodec, available)
                .Should().Be(expected);
        }

        [Fact]
        public void FindBestVideoEncoder_ReturnsNull_WhenUnavailableForFormat()
        {
            // webm-only list: hevc no tiene match de copy
            var available = new[] { "libvpx-vp9", "libvpx", "libaom-av1" };

            CodecMatchHelper.FindBestVideoEncoder("hevc", available)
                .Should().BeNull();
        }
    }
}
