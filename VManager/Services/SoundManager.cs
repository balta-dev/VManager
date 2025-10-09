using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using NAudio.Wave;

namespace VManager.Services
{
    public static class SoundManager
    {
        private static bool _enabled = true;

        public static bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled != value)
                {
                    _enabled = value;
                    System.Console.WriteLine($"Sonidos {(value ? "activados" : "desactivados")}");
                }
            }
        }
        
        
        private const string SoundsNamespace = "VManager.Assets.Sounds";
        
        public static async Task Play(string fileName)
        {
            if (!Enabled)
                return;
            
            if (string.IsNullOrWhiteSpace(fileName))
            {
                LogError("El nombre del archivo no puede estar vacío");
                return;
            }

            string resourceName = $"{SoundsNamespace}.{fileName}";
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                using var stream = assembly.GetManifestResourceStream(resourceName)
                    ?? throw new InvalidOperationException($"Recurso {resourceName} no encontrado");

                if (OperatingSystem.IsWindows())
                {
                    await PlaySoundAsync(stream, fileName, "Windows");
                }
                else if (OperatingSystem.IsLinux())
                {
                    string tempFilePath = Path.Combine(Path.GetTempPath(), fileName);
                    await ExtractResourceToTempFile(stream, tempFilePath, fileName);
                    await PlayUnixSoundAsync("aplay", tempFilePath, fileName);
                    CleanUpTempFile(tempFilePath);
                }
                else if (OperatingSystem.IsMacOS())
                {
                    string tempFilePath = Path.Combine(Path.GetTempPath(), fileName);
                    await ExtractResourceToTempFile(stream, tempFilePath, fileName);
                    await PlayUnixSoundAsync("afplay", tempFilePath, fileName);
                    CleanUpTempFile(tempFilePath);
                }
                else
                {
                    LogWarning("Sistema operativo no soportado para reproducir sonido");
                }
            }
            catch (Exception ex)
            {
                LogError($"Error al procesar el recurso {fileName}: {ex.Message}");
            }
        }

        private static async Task ExtractResourceToTempFile(Stream stream, string tempFilePath, string fileName)
        {
            try
            {
                using var fileStream = File.Create(tempFilePath);
                await stream.CopyToAsync(fileStream);
                //LogDebug($"Recurso extraído: {tempFilePath}");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"No se pudo extraer el recurso {fileName}: {ex.Message}", ex);
            }
        }

        private static async Task PlaySoundAsync(Stream stream, string fileName, string platform)
        {
            //LogDebug($"[{platform}] Reproduciendo: {fileName}");
            try
            {
                using var memoryStream = new MemoryStream();
                await stream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                using var waveOut = new WaveOutEvent();
                using var waveReader = new WaveFileReader(memoryStream);
                waveOut.Init(waveReader);
                await Task.Run(() =>
                {
                    waveOut.Play();
                    while (waveOut.PlaybackState == PlaybackState.Playing)
                    {
                        Task.Delay(100).Wait();
                    }
                });
                LogInfo($"[{platform}] Sonido reproducido: {fileName}");
            }
            catch (Exception ex)
            {
                LogError($"[{platform}] Error al reproducir {fileName}: {ex.Message}");
            }
        }

        private static async Task PlayUnixSoundAsync(string playerCommand, string filePath, string fileName)
        {
            //LogDebug($"[{playerCommand.ToUpper()}] Reproduciendo: {fileName}");
            try
            {
                using var process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = playerCommand,
                        Arguments = $"\"{filePath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };

                process.Start();
                await process.WaitForExitAsync();
                LogInfo($"[{playerCommand.ToUpper()}] Sonido reproducido: {fileName}");
            }
            catch (Exception ex)
            {
                LogError($"[{playerCommand.ToUpper()}] Error al reproducir {fileName}: {ex.Message}");
            }
        }

        private static void CleanUpTempFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al eliminar archivo temporal {filePath}: {ex.Message}");
            }
        }

        private static void LogDebug(string message) => Console.WriteLine($"[DEBUG] {message}");
        private static void LogInfo(string message) => Console.WriteLine($"[INFO] {message}");
        private static void LogWarning(string message) => Console.WriteLine($"[WARN] {message}");
        private static void LogError(string message) => Console.WriteLine($"[ERROR] {message}");
    }
}