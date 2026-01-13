using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Media;
using ReactiveUI;
using VManager.Services.Core.Converters;
using VManager.Services.Models;

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
            var config = JsonSerializer.Deserialize<AppConfig>(json, VManagerJsonContext.Default.AppConfig) ?? new AppConfig();
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
            var json = JsonSerializer.Serialize(config, VManagerJsonContext.Default.AppConfig)!;
            File.WriteAllText(ConfigPath, json);
        }
        catch
        {
            // Manejar errores según convenga
        }
    }
}
