using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
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

    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with RequiresUnreferencedCodeAttribute",
        Justification = "Manipulación de StreamHandlers es opcional y está protegida por comprobaciones en tiempo de ejecución")]
    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Evitar tocar DataValidators si no está disponible (trimming-safe)
            if (Avalonia.Data.Core.Plugins.BindingPlugins.StreamHandlers.Count > 0)
            {
                // Opcional: aquí se puede manipular StreamHandlers si realmente es necesario
            }

            var mainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel()
            };
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
            return;

        try
        {
            if (File.Exists(tempUpdaterPath))
            {
                foreach (var p in Process.GetProcessesByName("Updater"))
                {
                    try
                    {
                        p.Kill();
                        p.WaitForExit(5000);
                    }
                    catch { }
                }

                File.Copy(tempUpdaterPath, targetUpdaterPath, overwrite: true);
                Directory.Delete(tempFolder, true);
            }
        }
        catch { }
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
                return false;
            }
        }
    }
}
