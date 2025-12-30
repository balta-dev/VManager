using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Media;
using ReactiveUI;

namespace VManager.Services;

public static class ConfigurationService
{
    private static readonly string ConfigPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "VManager",
        "config.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters = { new ColorJsonConverter() }
    };

    // --- Instancia única compartida ---
    public static AppConfig Current { get; private set; } = LoadInternal();

    // --- Clase de configuración ---
    public class AppConfig : ReactiveObject
    {
        private string _language = "Español";
        public string Language
        {
            get => _language;
            set => this.RaiseAndSetIfChanged(ref _language, value);
        }

        private bool _enableSounds = true;
        public bool EnableSounds
        {
            get => _enableSounds;
            set => this.RaiseAndSetIfChanged(ref _enableSounds, value);
        }

        private bool _enableNotifications = true;
        public bool EnableNotifications
        {
            get => _enableNotifications;
            set => this.RaiseAndSetIfChanged(ref _enableNotifications, value);
        }

        private bool _useCustomIcon;
        public bool UseCustomIcon
        {
            get => _useCustomIcon;
            set => this.RaiseAndSetIfChanged(ref _useCustomIcon, value);
        }

        private bool _hideRemainingTime;
        public bool HideRemainingTime
        {
            get => _hideRemainingTime;
            set => this.RaiseAndSetIfChanged(ref _hideRemainingTime, value);
        }

        private Color? _selectedColor;
        [JsonConverter(typeof(ColorJsonConverter))]
        public Color? SelectedColor
        {
            get => _selectedColor;
            set => this.RaiseAndSetIfChanged(ref _selectedColor, value);
        }

        private string? _profileImagePath;
        public string? ProfileImagePath
        {
            get => _profileImagePath;
            set => this.RaiseAndSetIfChanged(ref _profileImagePath, value);
        }

        private string? _preferredDownloadFolder;
        public string? PreferredDownloadFolder
        {
            get => _preferredDownloadFolder;
            set => this.RaiseAndSetIfChanged(ref _preferredDownloadFolder, value);
        }

        // === Cookies ===
        private bool _useCookiesFile;
        public bool UseCookiesFile
        {
            get => _useCookiesFile;
            set => this.RaiseAndSetIfChanged(ref _useCookiesFile, value);
        }

        private string? _cookiesFilePath;
        public string? CookiesFilePath
        {
            get => _cookiesFilePath;
            set => this.RaiseAndSetIfChanged(ref _cookiesFilePath, value);
        }

        private DateTime? _cookiesLastUpdated;
        public DateTime? CookiesLastUpdated
        {
            get => _cookiesLastUpdated;
            set => this.RaiseAndSetIfChanged(ref _cookiesLastUpdated, value);
        }
        
        // === Log ===
        private bool _log;
        public bool Log
        {
            get => _log;
            set => this.RaiseAndSetIfChanged(ref _log, value);
        }
    }

    // --- Cargar desde disco ---
    private static AppConfig LoadInternal()
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
            return config;
        }
        catch
        {
            return new AppConfig();
        }
    }

    // --- Guardar en disco ---
    public static void Save()
    {
        Save(Current);
    }

    public static void Save(AppConfig config)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            var json = JsonSerializer.Serialize(config, JsonOptions);
            File.WriteAllText(ConfigPath, json);
        }
        catch
        {
            // Manejar errores según convenga
        }
    }
}
