using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace VManager.Services
{
    public class CodecService : ICodecService
    {
        private readonly string _ffmpegPath;

        public CodecService()
        {
            _ffmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "bin", "ffmpeg", "ffmpeg");
            if (OperatingSystem.IsWindows())
                _ffmpegPath += ".exe";
        }

        public async Task<IReadOnlyList<string>> GetAvailableVideoCodecsAsync()
        {
            var codecs = await GetCodecsAsync();
            return codecs
                .Where(c => c.Contains("264") || c.Contains("265") || c == "copy"
                         || c.Contains("vp8") || c.Contains("vp9")
                         || c.Contains("nvenc") || c.Contains("amf") || c.Contains("qsv"))
                .ToList();
        }

        public async Task<IReadOnlyList<string>> GetAvailableAudioCodecsAsync()
        {
            var codecs = await GetCodecsAsync();
            return codecs
                .Where(c => c.Contains("aac") || c.Contains("mp3")
                         || c == "copy" || c.Contains("opus"))
                .ToList();
        }

        private async Task<List<string>> GetCodecsAsync()
        {
            var psi = new ProcessStartInfo
            {
                FileName = _ffmpegPath,
                Arguments = "-codecs",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            var result = new List<string>();

            using var process = new Process { StartInfo = psi };
            process.Start();
            string output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                var lines = output.Split('\n');
                foreach (var line in lines)
                {
                    if (line.Length > 8 && (line[1] == 'D' || line[1] == 'E'))
                    {
                        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length > 1)
                            result.Add(parts[1]);
                    }
                }
            }

            return result;
        }
    }
}
