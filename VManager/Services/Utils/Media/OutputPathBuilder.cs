// Services/Utils/OutputPathBuilder.cs
using System.IO;
using VManager.Services; // para ProcessingConstants si lo dejaste en VideoProcessor

namespace VManager.Services.Utils.Media
{
    public static class OutputPathBuilder
    {
        public static string GetCutOutputPath(string inputPath)
        {
            string dir = Path.GetDirectoryName(inputPath)!;
            string name = Path.GetFileNameWithoutExtension(inputPath);
            string ext = Path.GetExtension(inputPath);
            return Path.Combine(dir, $"{name}{ProcessingConstants.CutSuffix}{ext}");
        }

        public static string GetCompressOutputPath(string inputPath, int percentage)
        {
            string dir = Path.GetDirectoryName(inputPath)!;
            string name = Path.GetFileNameWithoutExtension(inputPath);
            string ext = Path.GetExtension(inputPath);
            string suffix = string.Format(ProcessingConstants.CompressSuffixPattern, percentage);
            return Path.Combine(dir, $"{name}{suffix}{ext}");
        }

        public static string GetConvertOutputPath(string inputPath, string format)
        {
            string dir = Path.GetDirectoryName(inputPath)!;
            string name = Path.GetFileNameWithoutExtension(inputPath);
            return Path.Combine(dir, $"{name}{ProcessingConstants.ConvertSuffix}.{format}");
        }

        public static string GetAudioOutputPath(string inputPath, string audioFormat)
        {
            string dir = Path.GetDirectoryName(inputPath)!;
            string name = Path.GetFileNameWithoutExtension(inputPath);
            string normalizedFormat = audioFormat.StartsWith(".") ? audioFormat : "." + audioFormat;
            return Path.Combine(dir, $"{name}{ProcessingConstants.AudioSuffix}{normalizedFormat}");
        }

        public static string GetTempDirectory(string inputPath)
        {
            string baseDir = Path.GetDirectoryName(inputPath)!;
            return Path.Combine(baseDir, ProcessingConstants.TempFolderName);
        }
    }
}