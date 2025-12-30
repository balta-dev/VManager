using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace VManager.Services
{
    public static class SimpleSoundPlayer
    {
        [DllImport("winmm.DLL", EntryPoint = "PlaySound", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool PlaySound(string szSound, IntPtr hMod, uint flags);

        private const uint SND_ASYNC = 0x0001;
        private const uint SND_FILENAME = 0x00020000;
        private const uint SND_NODEFAULT = 0x0002;  // agregado para mejor debugging

        public static bool PlayWav(string path)
        {
            return PlaySound(path, IntPtr.Zero, SND_ASYNC | SND_FILENAME | SND_NODEFAULT);
        }
    }
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
            
            // Genera un nombre único cada vez
            string uniqueFileName = $"{Guid.NewGuid():N}_{fileName}";
            string tempFilePath = Path.Combine(Path.GetTempPath(), uniqueFileName);

            try
            {
                await ExtractResourceToTempFile(stream, tempFilePath, fileName);

                bool success = SimpleSoundPlayer.PlayWav(tempFilePath);

                if (success)
                    LogInfo($"[Windows] Sonido iniciado: {fileName}");
                else
                {
                    int error = Marshal.GetLastWin32Error();
                    LogError($"[Windows] PlaySound falló (error {error}): {fileName}");
                }

                // Borramos después de 10 segundos (el sonido ya está en memoria de winmm)
                _ = Task.Delay(TimeSpan.FromSeconds(10)).ContinueWith(_ => CleanUpTempFile(tempFilePath));
            }
            catch (Exception ex)
            {
                LogError($"[Windows] Excepción: {ex.Message}");
                CleanUpTempFile(tempFilePath);
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