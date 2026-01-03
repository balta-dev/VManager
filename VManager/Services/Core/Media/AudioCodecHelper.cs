// Services/Utils/AudioCodecHelper.cs
using System.Collections.Generic;
using System;

namespace VManager.Services.Core.Media
{
    public enum AudioProcessingAction
    {
        Copy,
        Reencode
    }

    public struct AudioProcessingDecision
    {
        public AudioProcessingAction Action;
        public string Codec;
        public int Bitrate;
        public string Reason;
    }

    public static class AudioCodecHelper
    {
        private static string NormalizeFormatName(string input)
        {
            var codecToFormat = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["libmp3lame"] = "mp3",
                ["libvorbis"] = "ogg",
                ["libopus"] = "opus",
                ["pcm_s16le"] = "wav",
                ["pcm_s24le"] = "wav",
                ["pcm_f32le"] = "wav",
                ["wmav2"] = "wma"
            };

            return codecToFormat.TryGetValue(input, out var format) ? format : input.ToLowerInvariant();
        }

        public static AudioProcessingDecision DecideAudioProcessing(string originalCodec, string targetFormat)
        {
            var decision = new AudioProcessingDecision();
            string normalizedFormat = NormalizeFormatName(targetFormat);

            var compatibleMappings = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["aac"] = new() { "aac", "m4a", "mp4" },
                ["mp3"] = new() { "mp3" },
                ["flac"] = new() { "flac" },
                ["vorbis"] = new() { "ogg", "oga" },
                ["opus"] = new() { "opus", "ogg" },
                ["pcm_s16le"] = new() { "wav" },
                ["pcm_s24le"] = new() { "wav" },
                ["pcm_f32le"] = new() { "wav" },
                ["wmav2"] = new() { "wma" }
            };

            originalCodec = originalCodec.ToLowerInvariant();

            if (compatibleMappings.TryGetValue(originalCodec, out var compatibleFormats) && compatibleFormats.Contains(normalizedFormat))
            {
                decision.Action = AudioProcessingAction.Copy;
                decision.Codec = "copy";
                decision.Bitrate = 0;
                decision.Reason = $"Códec {originalCodec} compatible con formato {normalizedFormat}";
                return decision;
            }

            decision.Action = AudioProcessingAction.Reencode;
            decision.Reason = $"Códec {originalCodec} no compatible con {normalizedFormat}, recodificando";

            decision.Bitrate = normalizedFormat switch
            {
                "mp3" => 192,
                "aac" or "m4a" => 128,
                "ogg" or "oga" => 192,
                "flac" => 0,
                "wav" => 0,
                "opus" => 128,
                "wma" => 128,
                _ => 128
            };

            decision.Codec = normalizedFormat switch
            {
                "mp3" => "libmp3lame",
                "aac" or "m4a" => "aac",
                "ogg" or "oga" => "libvorbis",
                "flac" => "flac",
                "wav" => "pcm_s16le",
                "opus" => "libopus",
                "wma" => "wmav2",
                _ => "aac"
            };

            return decision;
        }
    }
}