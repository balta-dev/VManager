using FluentAssertions;
using VManager.Services.Core.Media;
using Xunit;

namespace VManager.Tests.Unit
{
    public class HlsPlaylistHelperTests
    {
        [Fact]
        public void IsHlsPlaylist_DetectsExtM3U_EvenWithMp4Extension()
        {
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".mp4");
            try
            {
                File.WriteAllText(path, "#EXTM3U\n#EXT-X-VERSION:3\n");

                HlsPlaylistHelper.IsHlsPlaylist(path).Should().BeTrue();
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public void IsHlsPlaylist_RecognizesM3u8Extension()
        {
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".m3u8");
            try
            {
                File.WriteAllText(path, "#EXTM3U\n");

                HlsPlaylistHelper.IsHlsPlaylist(path).Should().BeTrue();
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [Fact]
        public void IsHlsPlaylist_ReturnsFalse_ForRegularBinaryLookingFile()
        {
            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".mp4");
            try
            {
                File.WriteAllBytes(path, new byte[] { 0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70 });

                HlsPlaylistHelper.IsHlsPlaylist(path).Should().BeFalse();
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}
