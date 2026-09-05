using System;
using System.Collections.Generic;
using System.Linq;

namespace VManager.Services.Core.Media
{
    /// <summary>
    /// Mapea nombres de códec de streams (ffprobe) a encoders disponibles en la UI,
    /// priorizando coincidencias que permitan <c>-c copy</c>.
    /// </summary>
    public static class CodecMatchHelper
    {
        public static string? FindBestVideoEncoder(string? streamCodecName, IEnumerable<string> availableEncoders)
        {
            if (string.IsNullOrWhiteSpace(streamCodecName))
                return null;

            var available = availableEncoders?.Where(e => !string.IsNullOrWhiteSpace(e)).ToList()
                            ?? new List<string>();
            if (available.Count == 0)
                return null;

            foreach (var candidate in GetVideoEncoderPreferences(streamCodecName))
            {
                var match = available.FirstOrDefault(e =>
                    e.Equals(candidate, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                    return match;
            }

            // Fallback: encoder cuyo nombre contiene el códec del stream (p.ej. hevc_nvenc)
            var codec = Normalize(streamCodecName);
            return available.FirstOrDefault(e =>
                e.Contains(codec, StringComparison.OrdinalIgnoreCase)
                || (codec is "h264" or "avc" && e.Contains("264", StringComparison.OrdinalIgnoreCase))
                || (codec is "hevc" or "h265" && (e.Contains("265", StringComparison.OrdinalIgnoreCase)
                                                   || e.Contains("hevc", StringComparison.OrdinalIgnoreCase))));
        }

        public static string? FindBestAudioEncoder(string? streamCodecName, IEnumerable<string> availableEncoders)
        {
            if (string.IsNullOrWhiteSpace(streamCodecName))
                return null;

            var available = availableEncoders?.Where(e => !string.IsNullOrWhiteSpace(e)).ToList()
                            ?? new List<string>();
            if (available.Count == 0)
                return null;

            foreach (var candidate in GetAudioEncoderPreferences(streamCodecName))
            {
                var match = available.FirstOrDefault(e =>
                    e.Equals(candidate, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                    return match;
            }

            var codec = Normalize(streamCodecName);
            return available.FirstOrDefault(e =>
                e.Equals(codec, StringComparison.OrdinalIgnoreCase)
                || e.Contains(codec, StringComparison.OrdinalIgnoreCase));
        }

        private static IEnumerable<string> GetVideoEncoderPreferences(string streamCodecName)
        {
            return Normalize(streamCodecName) switch
            {
                "h264" or "avc" => new[] { "libx264", "h264" },
                "hevc" or "h265" => new[] { "libx265", "hevc", "h265" },
                "vp9" => new[] { "libvpx-vp9", "vp9", "libvpx" },
                "vp8" => new[] { "libvpx", "vp8" },
                "av1" => new[] { "libsvtav1", "libaom-av1", "av1" },
                "mpeg4" => new[] { "mpeg4" },
                "mpeg2video" or "mpeg2" => new[] { "mpeg2video" },
                "prores" => new[] { "prores_ks", "prores" },
                "dnxhd" or "dnxhr" => new[] { "dnxhd", "dnxhr" },
                var other => new[] { other }
            };
        }

        private static IEnumerable<string> GetAudioEncoderPreferences(string streamCodecName)
        {
            return Normalize(streamCodecName) switch
            {
                "aac" => new[] { "aac" },
                "mp3" or "mp3float" => new[] { "libmp3lame", "mp3" },
                "opus" => new[] { "libopus", "opus" },
                "vorbis" => new[] { "libvorbis", "vorbis" },
                "flac" => new[] { "flac" },
                "wmav2" or "wma" => new[] { "wmav2" },
                "pcm_s16le" => new[] { "pcm_s16le" },
                "pcm_s24le" => new[] { "pcm_s24le" },
                "pcm_f32le" => new[] { "pcm_f32le" },
                var other when other.StartsWith("pcm_", StringComparison.Ordinal) => new[] { other },
                var other => new[] { other }
            };
        }

        private static string Normalize(string codecName)
            => codecName.Trim().ToLowerInvariant();
    }
}
