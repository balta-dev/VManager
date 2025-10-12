using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Media;
using VManager.Services;

public class ConfigurationService
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VManager",
        "config.json");

    // Propiedad estática para acceso rápido
    private static bool _hideRemainingTime;
    public static bool HideRemainingTime
    {
        get => _hideRemainingTime;
        private set
        {
            if (_hideRemainingTime != value)
            {
                _hideRemainingTime = value;
                HideRemainingTimeChanged?.Invoke(null, value);
            }
        }
    }

    // Evento para notificar cambios
    public static event EventHandler<bool>? HideRemainingTimeChanged;
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new ColorJsonConverter() }
    };

    public class AppConfig
    {
        public string Language { get; set; } = "Español";
        public bool EnableSounds { get; set; } = true;
        public bool EnableNotifications { get; set; } = true;
        public bool UseCustomIcon { get; set; }
        public bool HideRemainingTime { get; set; }
        
        [JsonConverter(typeof(ColorJsonConverter))]
        public Color? SelectedColor { get; set; }
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
            var config = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions) ?? new AppConfig();
            
            // Actualizar propiedad estática
            HideRemainingTime = config.HideRemainingTime;
            
            return config;
        }
        catch
        {
            return new AppConfig();
        }
    }

    public static void Save(AppConfig config)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            var json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(ConfigPath, json);
            
            // Actualizar propiedad estática
            HideRemainingTime = config.HideRemainingTime;
        }
        catch
        {
            // Manejar errores según convenga
        }
    }
    
}