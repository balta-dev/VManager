using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using VManager.ViewModels;
using VManager.Views;
using VManager.Services;
using VManager.Splash;

namespace VManager;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // Suscribirse a cambios de tema para actualizar brushes automáticamente
        this.GetObservable(ActualThemeVariantProperty).Subscribe(_ => ApplyCustomTheme());
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026:Members annotated with RequiresUnreferencedCodeAttribute",
        Justification = "Manipulación de StreamHandlers es opcional y está protegida por comprobaciones en tiempo de ejecución")]
    public override void OnFrameworkInitializationCompleted()
        {
            Console.WriteLine($"[STARTUP] [{MainWindow.StartupStopwatch?.ElapsedMilliseconds}ms] OnFrameworkInitializationCompleted");

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Trabajo pesado fuera del hilo UI
                ExtractDefaultTheme();
                Console.WriteLine($"[STARTUP] [{MainWindow.StartupStopwatch?.ElapsedMilliseconds}ms] ExtractDefaultTheme ejecutado");
                
                var config = ConfigurationService.Current;
                Console.WriteLine($"[STARTUP] [{MainWindow.StartupStopwatch?.ElapsedMilliseconds}ms] Config cargada");

                var savedTheme = config.ThemeName ?? "Default";
                    
                Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    ThemeService.Instance.Apply(savedTheme);
                    Console.WriteLine($"[STARTUP] [{MainWindow.StartupStopwatch?.ElapsedMilliseconds}ms] ThemeService.Instance.Apply(savedTheme)");

                    Application.Current!.RequestedThemeVariant = config.UseDarkTheme.HasValue
                        ? (config.UseDarkTheme.Value ? ThemeVariant.Dark : ThemeVariant.Light)
                        : ThemeVariant.Default;
                    Console.WriteLine($"[STARTUP] [{MainWindow.StartupStopwatch?.ElapsedMilliseconds}ms] Tema claro/oscuro aplicado");

                    var vm = new MainWindowViewModel();
                    Console.WriteLine($"[STARTUP] [{MainWindow.StartupStopwatch?.ElapsedMilliseconds}ms] MainWindowViewModel creado");

                    var mainWindow = new MainWindow { DataContext = vm };
                    Console.WriteLine($"[STARTUP] [{MainWindow.StartupStopwatch?.ElapsedMilliseconds}ms] MainWindow creada");

                    desktop.MainWindow = mainWindow;
                    mainWindow.Show();
                    NativeSplash.Close();
                        
                });
                
            }
            Task.Run(HandleUpdaterTempFolder);
            base.OnFrameworkInitializationCompleted();
        }
    
    private void ExtractDefaultTheme()
    {
        var themesDir = Path.Combine(
            Path.GetDirectoryName(Environment.ProcessPath!)!,
            "Themes", "Default");

        Directory.CreateDirectory(themesDir);

        var assembly = typeof(App).Assembly;
        const string prefix = "VManager.Assets.Themes.Default.";

        foreach (var resourceName in assembly.GetManifestResourceNames()
                     .Where(n => n.StartsWith(prefix)))
        {
            var fileName = resourceName[prefix.Length..]; // ej: "Colors.axaml"
            var destFile = Path.Combine(themesDir, fileName);

            if (File.Exists(destFile)) continue;

            using var stream = assembly.GetManifestResourceStream(resourceName)!;
            using var fs = File.Create(destFile);
            stream.CopyTo(fs);
            Console.WriteLine($"[STARTUP] Extraído: {fileName}");
        }
    }

    public void ApplyCustomTheme(ThemeVariant? theme = null)
    {
        var actualTheme = theme ?? ActualThemeVariant;

        if (actualTheme == ThemeVariant.Dark)
        {
            TryGetResource("WindowBackgroundBrushDark", null, out var wb); Resources["WindowBackgroundBrush"] = wb;
            TryGetResource("PanelBackgroundBrushDark", null, out var pb); Resources["PanelBackgroundBrush"] = pb;
            TryGetResource("BorderBrushPrimaryDark", null, out var bb); Resources["BorderBrushPrimary"] = bb;
        }
        else
        {
            TryGetResource("WindowBackgroundBrushLight", null, out var wb); Resources["WindowBackgroundBrush"] = wb;
            TryGetResource("PanelBackgroundBrushLight", null, out var pb); Resources["PanelBackgroundBrush"] = pb;
            TryGetResource("BorderBrushPrimaryLight", null, out var bb); Resources["BorderBrushPrimary"] = bb;
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
