using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace VManager.Services.Core
{
    public static class SimpleSoundPlayer
    {
        [DllImport("winmm.DLL", EntryPoint = "PlaySound", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool PlaySound(string szSound, IntPtr hMod, uint flags);

        private const uint SND_ASYNC = 0x0001;
        private const uint SND_FILENAME = 0x00020000;
        private const uint SND_NODEFAULT = 0x0002;

        public static bool PlayWav(string path)
        {
            return PlaySound(path, IntPtr.Zero, SND_ASYNC | SND_FILENAME | SND_NODEFAULT);
        }
    }

    public static class SoundManager
    {
        private static bool _enabled = false;

        public static bool Enabled
        {
            get => _enabled;
            set
            {
                if (_enabled != value)
                {
                    _enabled = value;
                    Console.WriteLine($"Sonidos {(value ? "activados" : "desactivados")}");
                }
            }
        }

        private const string SoundsNamespace = "VManager.Assets.Sounds";

        // Carpeta persistente, hermana de "Themes" y "Binaries"
        private static string AppRoot => Path.GetDirectoryName(Environment.ProcessPath!)!;
        private static string SoundsCachePath => Path.Combine(AppRoot, "Sounds");

        // Evita que dos extracciones del mismo archivo se pisen la primera vez
        private static readonly SemaphoreSlim _extractLock = new(1, 1);

        public static async Task Play(string fileName)
        {
            if (!Enabled)
                return;

            if (string.IsNullOrWhiteSpace(fileName))
            {
                LogError("El nombre del archivo no puede estar vacío");
                return;
            }

            try
            {
                string persistentPath = await EnsureExtracted(fileName);
                if (persistentPath == null)
                    return;

                if (OperatingSystem.IsWindows())
                {
                    bool success = SimpleSoundPlayer.PlayWav(persistentPath);
                    if (success)
                        LogInfo($"[Windows] Sonido iniciado: {fileName}");
                    else
                    {
                        int error = Marshal.GetLastWin32Error();
                        LogError($"[Windows] PlaySound falló (error {error}): {fileName}");
                    }
                }
                else if (OperatingSystem.IsLinux())
                {
                    await PlayUnixSoundAsync("aplay", persistentPath, fileName);
                }
                else if (OperatingSystem.IsMacOS())
                {
                    await PlayUnixSoundAsync("afplay", persistentPath, fileName);
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

        // Extrae el recurso embebido a la carpeta persistente SOLO si no existe todavía
        private static async Task<string?> EnsureExtracted(string fileName)
        {
            Directory.CreateDirectory(SoundsCachePath);
            string persistentPath = Path.Combine(SoundsCachePath, fileName);

            if (File.Exists(persistentPath))
                return persistentPath;

            // Lock para que dos Play() concurrentes del mismo sonido nuevo no se pisen en la extracción inicial
            await _extractLock.WaitAsync();
            try
            {
                // Doble chequeo por si otro hilo ya extrajo mientras esperábamos el lock
                if (File.Exists(persistentPath))
                    return persistentPath;

                string resourceName = $"{SoundsNamespace}.{fileName}";
                var assembly = Assembly.GetExecutingAssembly();
                using var stream = assembly.GetManifestResourceStream(resourceName);

                if (stream == null)
                {
                    LogError($"Recurso {resourceName} no encontrado");
                    return null;
                }

                // Extraemos a un archivo temporal y lo movemos, para que otros procesos
                // nunca vean un archivo a medio escribir bajo el nombre final
                string tempPath = persistentPath + $".tmp_{Guid.NewGuid():N}";
                using (var fileStream = File.Create(tempPath))
                {
                    await stream.CopyToAsync(fileStream);
                }
                File.Move(tempPath, persistentPath, overwrite: true);

                LogInfo($"Sonido extraído y cacheado: {persistentPath}");
                return persistentPath;
            }
            catch (Exception ex)
            {
                LogError($"No se pudo extraer el recurso {fileName}: {ex.Message}");
                return null;
            }
            finally
            {
                _extractLock.Release();
            }
        }

        private static async Task PlayUnixSoundAsync(string playerCommand, string filePath, string fileName)
        {
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

        private static void LogDebug(string message) => Console.WriteLine($"[DEBUG] {message}");
        private static void LogInfo(string message) => Console.WriteLine($"[INFO] {message}");
        private static void LogWarning(string message) => Console.WriteLine($"[WARN] {message}");
        private static void LogError(string message) => Console.WriteLine($"[ERROR] {message}");
    }
}