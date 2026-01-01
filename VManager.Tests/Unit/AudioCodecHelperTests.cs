using FluentAssertions;
using VManager.Services.Utils.Media;
using Xunit;

namespace VManager.Tests.Unit
{
    public class AudioCodecHelperTests
    {
        // ─────────────────────────────────────────────
        // COMPATIBLE → COPY
        // ─────────────────────────────────────────────

        [Theory]
        [InlineData("aac", "mp4")]
        [InlineData("aac", "m4a")]
        [InlineData("mp3", "mp3")]
        [InlineData("flac", "flac")]
        [InlineData("pcm_s16le", "wav")]
        [InlineData("pcm_s24le", "wav")]
        [InlineData("pcm_f32le", "wav")]
        public void DecideAudioProcessing_CompatibleCodec_ReturnsCopy(
            string originalCodec,
            string targetFormat)
        {
            var result = AudioCodecHelper.DecideAudioProcessing(originalCodec, targetFormat);

            result.Action.Should().Be(AudioProcessingAction.Copy);
            result.Codec.Should().Be("copy");
            result.Bitrate.Should().Be(0);
            result.Reason.Should().NotBeNullOrWhiteSpace();
        }

        // ─────────────────────────────────────────────
        // INCOMPATIBLE → REENCODE
        // ─────────────────────────────────────────────

        [Theory]
        [InlineData("aac", "mp3", "libmp3lame", 192)]
        [InlineData("mp3", "aac", "aac", 128)]
        [InlineData("flac", "mp3", "libmp3lame", 192)]
        [InlineData("vorbis", "mp3", "libmp3lame", 192)]
        [InlineData("opus", "mp3", "libmp3lame", 192)]
        public void DecideAudioProcessing_IncompatibleCodec_Reencodes(
            string originalCodec,
            string targetFormat,
            string expectedCodec,
            int expectedBitrate)
        {
            var result = AudioCodecHelper.DecideAudioProcessing(originalCodec, targetFormat);

            result.Action.Should().Be(AudioProcessingAction.Reencode);
            result.Codec.Should().Be(expectedCodec);
            result.Bitrate.Should().Be(expectedBitrate);
            result.Reason.Should().NotBeNullOrWhiteSpace();
        }

        // ─────────────────────────────────────────────
        // NORMALIZACIÓN DE FORMATOS
        // ─────────────────────────────────────────────

        [Theory]
        [InlineData("pcm_s16le", "wav")]
        [InlineData("pcm_s24le", "wav")]
        [InlineData("pcm_f32le", "wav")]
        [InlineData("wmav2", "wma")]
        public void DecideAudioProcessing_NormalizesFormatNames(
            string originalCodec,
            string targetFormat)
        {
            var result = AudioCodecHelper.DecideAudioProcessing(originalCodec, targetFormat);

            result.Action.Should().Be(AudioProcessingAction.Copy);
            result.Codec.Should().Be("copy");
        }

        // ─────────────────────────────────────────────
        // CASE INSENSITIVE
        // ─────────────────────────────────────────────

        [Fact]
        public void DecideAudioProcessing_IsCaseInsensitive()
        {
            var result = AudioCodecHelper.DecideAudioProcessing("AAC", "MP4");

            result.Action.Should().Be(AudioProcessingAction.Copy);
            result.Codec.Should().Be("copy");
        }

        // ─────────────────────────────────────────────
        // FORMATO DESCONOCIDO → FALLBACK
        // ─────────────────────────────────────────────

        [Fact]
        public void DecideAudioProcessing_UnknownFormat_UsesDefaultFallback()
        {
            var result = AudioCodecHelper.DecideAudioProcessing("aac", "unknownformat");

            result.Action.Should().Be(AudioProcessingAction.Reencode);
            result.Codec.Should().Be("aac");
            result.Bitrate.Should().Be(128);
        }

        // ─────────────────────────────────────────────
        // INVARIANTE CRÍTICA
        // ─────────────────────────────────────────────

        [Fact]
        public void DecideAudioProcessing_CopyAlwaysHasZeroBitrate()
        {
            var result = AudioCodecHelper.DecideAudioProcessing("mp3", "mp3");

            result.Action.Should().Be(AudioProcessingAction.Copy);
            result.Bitrate.Should().Be(0);
        }
    }
}
