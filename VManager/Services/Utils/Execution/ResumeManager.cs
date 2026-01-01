using System;
using System.IO;
using System.Text.Json;
using VManager.Services.Utils.Media;

namespace VManager.Services.Utils.Execution
{
    public static class ResumeManager
    {
        public static bool IsChunkValid(string chunkPath)
        {
            if (!File.Exists(chunkPath)) return false;

            try
            {
                var info = new FileInfo(chunkPath);
                // Un chunk de video de 60s procesado debe pesar algo razonable.
                // Si pesa 0 o muy pocos bytes, está corrupto por un crash.
                if (info.Length < 1024) return false; 
        
                return true;
            }
            catch
            {
                return false;
            }
        }
        public static string GetTempFolder(string inputPath)
        {
            return OutputPathBuilder.GetTempDirectory(inputPath);
        }

        private static string GetProgressLogPath(string inputPath)
        {
            string tempFolder = GetTempFolder(inputPath);
            return Path.Combine(tempFolder, ProcessingConstants.ProgressLogFile);
        }

        public static ResumeProgress? LoadProgress(string inputPath)
        {
            string logPath = GetProgressLogPath(inputPath);
            if (!File.Exists(logPath)) return null;

            try
            {
                string json = File.ReadAllText(logPath);
                return JsonSerializer.Deserialize<ResumeProgress>(json);
            }
            catch
            {
                return null;
            }
        }

        public static void SaveProgress(string inputPath, ResumeProgress progress)
        {
            string tempFolder = GetTempFolder(inputPath);
            Directory.CreateDirectory(tempFolder);

            string json = JsonSerializer.Serialize(progress, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(GetProgressLogPath(inputPath), json);
        }

        public static void ClearProgress(string inputPath)
        {
            string logPath = GetProgressLogPath(inputPath);
            if (File.Exists(logPath)) File.Delete(logPath);

            string tempFolder = GetTempFolder(inputPath);
            if (Directory.Exists(tempFolder))
            {
                try { Directory.Delete(tempFolder, true); }
                catch { }
            }
        }
    }
}