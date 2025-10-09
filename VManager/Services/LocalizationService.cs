using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace VManager.Services;

public class LocalizationService : INotifyPropertyChanged
{
    private static LocalizationService? _instance;
    public static LocalizationService Instance => _instance ??= new();
    
    private readonly Dictionary<string, Dictionary<string, string>> _translations = new();
    private string _currentLanguage = "es";
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    public LocalizationService()
    {
        LoadTranslations();
    }
    
    private void LoadTranslations()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resources = assembly.GetManifestResourceNames()
            .Where(r => r.Contains("Localization") && r.EndsWith(".json"))
            .ToList();

        System.Console.WriteLine("Recursos encontrados:");
        foreach (var r in resources)
            System.Console.WriteLine($" - {r}");

        foreach (var resource in resources)
        {
            var parts = resource.Split('.');
            var lang = parts[^2]; // <- usa esto
            using var stream = assembly.GetManifestResourceStream(resource);
            if (stream is null)
            {
                System.Console.WriteLine($"[WARN] No se pudo cargar el recurso '{resource}'.");
                continue;
            }
            using var reader = new StreamReader(stream);
            var json = reader.ReadToEnd();

            using var doc = JsonDocument.Parse(json);
            var flatDict = new Dictionary<string, string>();
            FlattenJson(doc.RootElement, flatDict);
            _translations[lang] = flatDict;

            System.Console.WriteLine($"Idioma '{lang}' cargado con {_translations[lang].Count} entradas");
        }
    }

    private void FlattenJson(JsonElement element, Dictionary<string, string> dict, string prefix = "")
    {
        foreach (var property in element.EnumerateObject())
        {
            string key = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}";
            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                FlattenJson(property.Value, dict, key);
            }
            else
            {
                dict[key] = property.Value.ToString();
            }
        }
    }
    
    public string CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (_currentLanguage != value && _translations.ContainsKey(value))
            {
                _currentLanguage = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLanguage)));
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            }
        }
    }
    
    public string this[string key]
    {
        get
        {
            if (_translations.TryGetValue(_currentLanguage, out var translations) &&
                translations.TryGetValue(key, out var value))
                return value;

            // fallback a español
            if (_currentLanguage != "es" &&
                _translations.TryGetValue("es", out var defaultTranslations) &&
                defaultTranslations.TryGetValue(key, out var fallbackValue))
                return fallbackValue;

            return $"[{key}]";
        }
    }
}