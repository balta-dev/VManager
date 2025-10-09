using System;
using System.IO;
using System.Text.Json;

namespace VManager.Services;

public class ConfigurationService
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VManager",
        "config.json");

    public class AppConfig
    {
        public string Language { get; set; } = "Español";          // Idioma
        public bool EnableSounds { get; set; } = true;             // Sonidos activados/desactivados
        public bool EnableNotifications { get; set; } = true;      // Notificaciones activadas/desactivadas
        public bool UseCustomIcon { get; set; } = true;            // Usar icono personalizado
    }
    public static AppConfig Load()
    {
        try
        {
            if (!File.Exists(ConfigPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
                var defaultConfig = new AppConfig();
                Save(defaultConfig);
                return defaultConfig;
            }

            var json = File.ReadAllText(ConfigPath);
            return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
        }
        catch
        {
            // Si algo falla, devolver configuración por defecto
            return new AppConfig();
        }
    }

    public static void Save(AppConfig config)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            var json = JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch
        {
            // Manejar errores según convenga (log, notificación, etc.)
        }
    }
}