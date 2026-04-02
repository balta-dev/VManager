using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;

namespace VManager.Services;

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

    [UnconditionalSuppressMessage("Trimming", "IL2026")]
    public void Apply(string themeName)
    {
        var folder = Path.Combine(ThemesPath, themeName);
        if (!Directory.Exists(folder))
            folder = DefaultThemePath;

        // Limpiar recursos del tema anterior
        foreach (var d in _loadedResources)
            Application.Current!.Resources.MergedDictionaries.Remove(d);
        _loadedResources.Clear();

        // Limpiar estilos del tema anterior
        foreach (var s in _loadedStyles)
            Application.Current!.Styles.Remove(s);
        _loadedStyles.Clear();
        
        // Cargar todos los .axaml de la carpeta en orden alfabético
        var files = Directory.GetFiles(folder, "*.axaml");

        foreach (var file in files)
        {
            try
            {
                var xaml = File.ReadAllText(file);
                var loaded = AvaloniaRuntimeXamlLoader.Load(xaml);

                switch (loaded)
                {
                    case IResourceProvider dict:
                        Application.Current!.Resources.MergedDictionaries.Add(dict);
                        _loadedResources.Add(dict);
                        Console.WriteLine($"[ThemeService] Recurso cargado: {Path.GetFileName(file)}");
                        break;
                    case IStyle style:
                        Application.Current!.Styles.Add(style);
                        _loadedStyles.Add(style);
                        Console.WriteLine($"[ThemeService] Estilo cargado: {Path.GetFileName(file)}");
                        break;
                    default:
                        Console.WriteLine($"[ThemeService] Tipo desconocido en: {Path.GetFileName(file)}");
                        break;
                }
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException?.Message ?? "sin inner exception";
                Console.WriteLine($"[ThemeService] Error en {Path.GetFileName(file)}: {ex.Message} | Inner: {inner}");
            }
        }

        if (Application.Current is App app)
            app.ApplyCustomTheme();

        Console.WriteLine($"[ThemeService] Tema aplicado: {themeName}");
    }
}