using System;
using System.IO;
using System.Text;
using FFMpegCore;

namespace VManager.Services.Core.Media
{
    /// <summary>
    /// Detecta playlists HLS (M3U8), incluso si se guardaron con extensión incorrecta (p.ej. .mp4).
    /// </summary>
    public static class HlsPlaylistHelper
    {
        public const string ProtocolWhitelist = "file,http,https,tcp,tls,crypto";

        /// <summary>Argumentos extra para ffprobe al analizar un playlist HLS local/remoto.</summary>
        public static string FfprobeCustomArguments =>
            $"-protocol_whitelist {ProtocolWhitelist} -f hls";

        public static bool IsHlsPlaylist(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return false;

            // URLs HLS remotas
            if (Uri.TryCreate(path, UriKind.Absolute, out var uri)
                && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            {
                var ext = Path.GetExtension(uri.AbsolutePath);
                if (ext.Equals(".m3u8", StringComparison.OrdinalIgnoreCase)
                    || ext.Equals(".m3u", StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            if (!File.Exists(path))
                return false;

            var fileExt = Path.GetExtension(path);
            if (fileExt.Equals(".m3u8", StringComparison.OrdinalIgnoreCase)
                || fileExt.Equals(".m3u", StringComparison.OrdinalIgnoreCase))
                return true;

            // Sniff: muchos descargadores guardan el playlist como .mp4
            try
            {
                using var fs = File.OpenRead(path);
                Span<byte> buffer = stackalloc byte[32];
                int read = fs.Read(buffer);
                if (read <= 0)
                    return false;

                var header = Encoding.UTF8.GetString(buffer[..read])
                    .TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
                return header.StartsWith("#EXTM3U", StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Construye FFMpegArguments con flags necesarios para HLS.</summary>
        public static FFMpegArguments CreateInputArguments(string inputPath, bool isHls)
        {
            return FFMpegArguments.FromFileInput(inputPath, verifyExists: File.Exists(inputPath), options =>
            {
                if (!isHls)
                    return;

                options.ForceFormat("hls");
                options.WithCustomArgument($"-protocol_whitelist {ProtocolWhitelist}");
            });
        }
    }
}
