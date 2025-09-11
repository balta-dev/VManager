using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace VManager.Services
{
    public static class SoundManager
    {
        private static readonly string _basePath;

        static SoundManager()
        {
            try
            {
                _basePath = Path.Combine(
                    Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(Path.GetDirectoryName(AppContext.BaseDirectory)))),
                    "Assets", "Sounds"
                );
            }
            catch
            {
                _basePath = AppContext.BaseDirectory;
            }
        }

        public static void Play(string fileName)
        {
            string soundPath = Path.Combine(_basePath, fileName);

            if (!File.Exists(soundPath))
                return;

            Task.Run(() =>
            {
                try
                {
                    if (OperatingSystem.IsLinux())
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "aplay",
                            Arguments = $"\"{soundPath}\"",
                            UseShellExecute = false,
                            CreateNoWindow = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true
                        });
                    }
                    else if (OperatingSystem.IsWindows())
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "powershell",
                            Arguments = $"-c \"(New-Object Media.SoundPlayer '{soundPath}').PlaySync()\"",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reproduciendo sonido {fileName}: {ex.Message}");
                }
            });
        }
    }
}
