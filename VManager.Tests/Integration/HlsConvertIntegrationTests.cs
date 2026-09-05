using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using VManager.Services;
using VManager.Services.Core.Media;
using VManager.Services.Operations;
using Xunit;

namespace VManager.Tests.Integration
{
    public class HlsConvertIntegrationTests : IAsyncLifetime
    {
        private const string TestFilesDir = "HlsIntegrationTestFiles";
        private string _ffmpegPath = string.Empty;

        public async Task InitializeAsync()
        {
            await FFmpegManager.Initialize();
            _ffmpegPath = FFmpegManager.FfmpegPath;

            if (Directory.Exists(TestFilesDir))
                Directory.Delete(TestFilesDir, true);
            Directory.CreateDirectory(TestFilesDir);
        }

        public Task DisposeAsync()
        {
            if (Directory.Exists(TestFilesDir))
                Directory.Delete(TestFilesDir, true);
            return Task.CompletedTask;
        }

        [Fact]
        public async Task Convert_MisnamedHlsPlaylist_ProducesPlayableMp4()
        {
            // Playlist local con un solo segmento generado (sin red)
            string playlistPath = Path.Combine(TestFilesDir, "fake_stream.mp4"); // extensión incorrecta a propósito
            string segmentPath = Path.Combine(TestFilesDir, "seg0.ts");
            string outputPath = Path.Combine(TestFilesDir, "out.mp4");

            // Generar un segmento MPEG-TS corto
            var genArgs = $"-f lavfi -i testsrc=duration=2:size=160x120:rate=10 -f lavfi -i sine=frequency=440:duration=2 -c:v libx264 -pix_fmt yuv420p -c:a aac -f mpegts -y \"{segmentPath}\"";
            var gen = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = genArgs,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            })!;
            await gen.WaitForExitAsync();
            gen.ExitCode.Should().Be(0, await gen.StandardError.ReadToEndAsync());

            await File.WriteAllTextAsync(playlistPath,
                "#EXTM3U\n#EXT-X-VERSION:3\n#EXT-X-TARGETDURATION:3\n#EXT-X-MEDIA-SEQUENCE:0\n#EXTINF:2.0,\n"
                + Path.GetFullPath(segmentPath).Replace("\\", "/") + "\n#EXT-X-ENDLIST\n");

            HlsPlaylistHelper.IsHlsPlaylist(playlistPath).Should().BeTrue();

            var operation = new ConvertOperation(_ffmpegPath);
            var result = await operation.ExecuteAsync(
                playlistPath,
                outputPath,
                videoCodec: "libx264",
                audioCodec: "aac",
                selectedFormat: "mp4",
                progress: new Progress<IFFmpegProcessor.ProgressInfo>(),
                cancellationToken: CancellationToken.None
            );

            result.Success.Should().BeTrue($"HLS convert failed: {result.Message}");
            File.Exists(outputPath).Should().BeTrue();
            new FileInfo(outputPath).Length.Should().BeGreaterThan(0);
        }
    }
}
