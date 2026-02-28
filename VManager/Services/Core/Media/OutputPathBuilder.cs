// Services/Utils/OutputPathBuilder.cs
using System.IO;
using VManager.Services; // para ProcessingConstants si lo dejaste en VideoProcessor

namespace VManager.Services.Core.Media
{
    public static class OutputPathBuilder
    {
        public static string SanitizeFilename(string path)
        {
            var dir = Path.GetDirectoryName(path)!;
            var name = Path.GetFileName(path)
                .Replace("\"", "'")
                .Replace("/", "-")
                .Replace("\\", "-");
            var newPath = Path.Combine(dir, name);

            if (path != newPath && File.Exists(path))
                File.Move(path, newPath);

            return newPath;
        }
        
        public static string GetCutOutputPath(string inputPath)
        {
            inputPath = SanitizeFilename(inputPath);
            string dir = Path.GetDirectoryName(inputPath)!;
            string name = Path.GetFileNameWithoutExtension(inputPath);
            string ext = Path.GetExtension(inputPath);
            return Path.Combine(dir, $"{name}{ProcessingConstants.CutSuffix}{ext}");
        }

        public static string GetCompressOutputPath(string inputPath, int percentage)
        {
            inputPath = SanitizeFilename(inputPath);
            string dir = Path.GetDirectoryName(inputPath)!;
            string name = Path.GetFileNameWithoutExtension(inputPath);
            string ext = Path.GetExtension(inputPath);
            string suffix = string.Format(ProcessingConstants.CompressSuffixPattern, percentage);
            return Path.Combine(dir, $"{name}{suffix}{ext}");
        }

        public static string GetConvertOutputPath(string inputPath, string format)
        {
            inputPath = SanitizeFilename(inputPath);
            string dir = Path.GetDirectoryName(inputPath)!;
            string name = Path.GetFileNameWithoutExtension(inputPath);
            return Path.Combine(dir, $"{name}{ProcessingConstants.ConvertSuffix}.{format}");
        }

        public static string GetAudioOutputPath(string inputPath, string audioFormat)
        {
            inputPath = SanitizeFilename(inputPath);
            string dir = Path.GetDirectoryName(inputPath)!;
            string name = Path.GetFileNameWithoutExtension(inputPath);
            string normalizedFormat = audioFormat.StartsWith(".") ? audioFormat : "." + audioFormat;
            return Path.Combine(dir, $"{name}{ProcessingConstants.AudioSuffix}{normalizedFormat}");
        }

        public static string GetTempDirectory(string inputPath)
        {
            inputPath = SanitizeFilename(inputPath);
            string baseDir = Path.GetDirectoryName(inputPath)!;
            return Path.Combine(baseDir, ProcessingConstants.TempFolderName);
        }
    }
}