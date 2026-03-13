using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;

namespace VManager.Services.Core;

public class LocalizationService : INotifyPropertyChanged
{
    private static LocalizationService? _instance;
    public static LocalizationService Instance => _instance ??= new();
    
    private readonly Dictionary<string, Dictionary<string, string>> _translations = new();
    private string _currentLanguage = "es";
    
    public event PropertyChangedEventHandler? PropertyChanged;
    
    public LocalizationService()
    {
        //EnsureLanguageLoaded(_currentLanguage);
    }
    
    private void EnsureLanguageLoaded(string lang)
    {
        if (_translations.ContainsKey(lang))
            return;

        var assembly = Assembly.GetExecutingAssembly();
        
        var resource = assembly.GetManifestResourceNames()
            .FirstOrDefault(r => r.Contains("Localization") && r.EndsWith($"{lang}.json"));

        if (resource is null)
        {
            System.Console.WriteLine($"[WARN] Recurso para idioma '{lang}' no encontrado.");
            return;
        }

        using var stream = assembly.GetManifestResourceStream(resource);
        if (stream is null) return;

        using var reader = new StreamReader(stream);
        using var doc = JsonDocument.Parse(reader.ReadToEnd());
        
        var flatDict = new Dictionary<string, string>();
        FlattenJson(doc.RootElement, flatDict);
        _translations[lang] = flatDict;

        System.Console.WriteLine($"Idioma '{lang}' cargado con {_translations[lang].Count} entradas");
    }

    private void FlattenJson(JsonElement element, Dictionary<string, string> dict, string prefix = "")
    {
        foreach (var property in element.EnumerateObject())
        {
            string key = string.IsNullOrEmpty(prefix) ? property.Name : $"{prefix}.{property.Name}";
            if (property.Value.ValueKind == JsonValueKind.Object)
                FlattenJson(property.Value, dict, key);
            else
                dict[key] = property.Value.ToString();
        }
    }
    
    public string CurrentLanguage
    {
        get => _currentLanguage;
        set
        {
            if (_currentLanguage == value) return;
            
            EnsureLanguageLoaded(value);
            
            if (!_translations.ContainsKey(value))
                return;
                
            _currentLanguage = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLanguage)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        }
    }
    
    public string this[string key]
    {
        get
        {
            EnsureLanguageLoaded(_currentLanguage);
            
            if (_translations.TryGetValue(_currentLanguage, out var translations) &&
                translations.TryGetValue(key, out var value))
                return value;

            // fallback a español
            if (_currentLanguage != "es")
            {
                EnsureLanguageLoaded("es");
                if (_translations.TryGetValue("es", out var defaultTranslations) &&
                    defaultTranslations.TryGetValue(key, out var fallbackValue))
                    return fallbackValue;
            }

            return $"[{key}]";
        }
    }
}