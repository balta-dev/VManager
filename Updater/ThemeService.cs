using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using System.Text.Json;
using Avalonia.Media;

namespace Updater;

public class ThemeService
{
    public static readonly ThemeService Instance = new();

    private static string ThemesPath => Path.Combine(
        Path.GetDirectoryName(Environment.ProcessPath!)!, "Themes");

    private static string DefaultThemePath => Path.Combine(ThemesPath, "Default");

    private readonly List<IResourceProvider> _loadedResources = new();
    private readonly List<IStyle> _loadedStyles = new();

    public IEnumerable<string> GetThemes()
    {
        if (!Directory.Exists(ThemesPath))
        {
            Console.WriteLine($"[ThemeService] No existe ThemesPath: {ThemesPath}");
            return Enumerable.Empty<string>();
        }
        var dirs = Directory.GetDirectories(ThemesPath).Select(Path.GetFileName).ToList();
        Console.WriteLine($"[ThemeService] Temas encontrados: {string.Join(", ", dirs!)}");
        return dirs!;
    }
    
    private static void LogInvalid(string file, string key, string type, string value, Exception? ex = null)
    {
        Console.WriteLine(
            $"[ThemeService] Valor inválido ({type}) en {Path.GetFileName(file)} -> Key='{key}', Value='{value}'" +
            (ex != null ? $" | {ex.Message}" : "")
        );
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026")]
    public void Apply(string themeName)
    {
        var folder = Path.Combine(ThemesPath, themeName);
        if (!Directory.Exists(folder))
            folder = DefaultThemePath;

        var app = Application.Current!;

        // limpiar
        foreach (var d in _loadedResources)
            app.Resources.MergedDictionaries.Remove(d);
        _loadedResources.Clear();

        foreach (var s in _loadedStyles)
            app.Styles.Remove(s);
        _loadedStyles.Clear();

        var files = Directory.GetFiles(folder, "*.json");
        
        var options = new JsonDocumentOptions
        {
            CommentHandling = JsonCommentHandling.Skip
        };

        foreach (var file in files)
        {
            try
            {
                var json = File.ReadAllText(file);
                var doc = JsonDocument.Parse(json, options);

                var dict = new ResourceDictionary();

                // Brushes
                if (doc.RootElement.TryGetProperty("Brushes", out var brushes))
                {
                    foreach (var prop in brushes.EnumerateObject())
                    {
                        var str = prop.Value.GetString();
                        if (string.IsNullOrWhiteSpace(str))
                        {
                            LogInvalid(file, prop.Name, "Brush", "null o vacío");
                            continue;
                        }

                        try
                        {
                            var color = Color.Parse(str);
                            dict[prop.Name] = new SolidColorBrush(color);
                        }
                        catch (Exception ex)
                        {
                            LogInvalid(file, prop.Name, "Brush", str, ex);
                        }
                    }
                }

                // CornerRadius
                if (doc.RootElement.TryGetProperty("CornerRadius", out var corners))
                {
                    foreach (var prop in corners.EnumerateObject())
                    {
                        if (!prop.Value.TryGetDouble(out var v))
                        {
                            LogInvalid(file, prop.Name, "CornerRadius", prop.Value.ToString());
                            continue;
                        }

                        dict[prop.Name] = new CornerRadius(v);
                    }
                }

                // Double
                if (doc.RootElement.TryGetProperty("Double", out var doubles))
                {
                    foreach (var prop in doubles.EnumerateObject())
                    {
                        if (!prop.Value.TryGetDouble(out var v))
                        {
                            LogInvalid(file, prop.Name, "Double", prop.Value.ToString());
                            continue;
                        }

                        dict[prop.Name] = v;
                    }
                }

                // Thickness
                if (doc.RootElement.TryGetProperty("Thickness", out var thicknesses))
                {
                    foreach (var prop in thicknesses.EnumerateObject())
                    {
                        var str = prop.Value.GetString();
                        if (string.IsNullOrWhiteSpace(str))
                        {
                            LogInvalid(file, prop.Name, "Thickness", "null o vacío");
                            continue;
                        }

                        try
                        {
                            dict[prop.Name] = Thickness.Parse(str);
                        }
                        catch (Exception ex)
                        {
                            LogInvalid(file, prop.Name, "Thickness", str, ex);
                        }
                    }
                }

                app.Resources.MergedDictionaries.Add(dict);
                _loadedResources.Add(dict);

                Console.WriteLine($"[ThemeService] JSON cargado: {Path.GetFileName(file)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ThemeService] Error JSON {file}: {ex.Message}");
            }
        }

        if (app is App a)
            a.ApplyCustomTheme();

        Console.WriteLine($"[ThemeService] Tema aplicado: {themeName}");
    }
}