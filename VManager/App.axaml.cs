using System;
using System.Diagnostics;
using System.IO;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.Threading;
using VManager.ViewModels;
using VManager.Views;

namespace VManager;

public partial class App : Application
{
    public override void Initialize()
    {
        HandleUpdaterTempFolder();
        AvaloniaXamlLoader.Load(this);
        
        // Suscribirse a cambios de tema para actualizar brushes automáticamente
        this.GetObservable(ActualThemeVariantProperty).Subscribe(_ => ApplyCustomTheme());
    }
    
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {

            if (BindingPlugins.DataValidators.Count > 0)
                BindingPlugins.DataValidators.RemoveAt(0);
            
            var mainWindow = new MainWindow { DataContext = new MainWindowViewModel(), };
            desktop.MainWindow = mainWindow;
            
        }
        base.OnFrameworkInitializationCompleted();
    }
    private void ApplyCustomTheme(ThemeVariant? theme = null)
    {
        var actualTheme = theme ?? ActualThemeVariant;

        if (actualTheme == ThemeVariant.Dark)
        {
            Resources["WindowBackgroundBrush"] = Resources["WindowBackgroundBrushDark"];
            Resources["PanelBackgroundBrush"] = Resources["PanelBackgroundBrushDark"];
            Resources["BorderBrushPrimary"] = Resources["BorderBrushPrimaryDark"];
        }
        else
        {
            Resources["WindowBackgroundBrush"] = Resources["WindowBackgroundBrushLight"];
            Resources["PanelBackgroundBrush"] = Resources["PanelBackgroundBrushLight"];
            Resources["BorderBrushPrimary"] = Resources["BorderBrushPrimaryLight"];
        }
    }
    
    private void HandleUpdaterTempFolder()
    {
        string tempFolder = Path.Combine(Path.GetTempPath(), "VManager_Update");
        string updaterFileName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "Updater.exe" : "Updater";
        string tempUpdaterPath = Path.Combine(tempFolder, updaterFileName);
        string targetUpdaterPath = Path.Combine(AppContext.BaseDirectory, updaterFileName);

        if (!Directory.Exists(tempFolder))
            return; // no hay nada que hacer

        try
        {
            if (File.Exists(tempUpdaterPath))
            {
                Console.WriteLine("Actualizando Updater desde carpeta temporal...");

                // 1️⃣ Matar Updater si está corriendo (por si acaso)
                foreach (var p in Process.GetProcessesByName("Updater"))
                {
                    try
                    {
                        p.Kill();
                        p.WaitForExit(5000);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"No se pudo cerrar Updater: {ex.Message}");
                    }
                }

                // 2️⃣ Reemplazar Updater
                File.Copy(tempUpdaterPath, targetUpdaterPath, overwrite: true);
                Console.WriteLine("Updater reemplazado correctamente.");

                // 3️⃣ Borrar carpeta temporal completa
                Directory.Delete(tempFolder, true);
                Console.WriteLine($"Carpeta temporal {tempFolder} eliminada.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al manejar carpeta temporal de Updater: {ex.Message}");
        }
    }
    
    public static class BuildInfo
    {
        public static bool IsSelfContained()
        {
            try
            {
                var baseDir = AppContext.BaseDirectory;
                return File.Exists(Path.Combine(baseDir, "hostfxr.dll")) &&
                       File.Exists(Path.Combine(baseDir, "coreclr.dll"));
            }
            catch
            {
                return false; // Por defecto, asume framework-dependent si hay error
            }
        }
    }

}
