using System;
using System.Collections.Generic;
using FFMpegCore;
using FFMpegCore.Builders.MetaData;

namespace VManager.Tests.Fakes
{
    internal class FakeMediaAnalysis : IMediaAnalysis
    {
        public TimeSpan Duration { get; set; } = TimeSpan.FromSeconds(60);
        public List<AudioStream> AudioStreams { get; set; } = new List<AudioStream>();
        public List<VideoStream> VideoStreams { get; set; } = new List<VideoStream>();
        public List<SubtitleStream> SubtitleStreams { get; set; } = new List<SubtitleStream>();
        public List<ChapterData> Chapters { get; set; } = new List<ChapterData>();
        public IReadOnlyList<string> ErrorData { get; set; } = Array.Empty<string>();
        public MediaFormat Format { get; set; } = new MediaFormat();
        public AudioStream? PrimaryAudioStream => AudioStreams.Count > 0 ? AudioStreams[0] : null;
        public VideoStream? PrimaryVideoStream => VideoStreams.Count > 0 ? VideoStreams[0] : null;
        public SubtitleStream? PrimarySubtitleStream => SubtitleStreams.Count > 0 ? SubtitleStreams[0] : null;
    }
}