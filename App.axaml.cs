using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Styling;
using VManager.ViewModels;
using VManager.Views;

namespace VManager;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // Suscribirse a cambios de tema para actualizar brushes automáticamente
        this.PropertyChanged += (s, e) =>
        {
            if (e.Property == Application.ActualThemeVariantProperty)
            {
                ApplyCustomTheme();
            }
        };
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Aplica tus brushes personalizados según el tema actual
            ApplyCustomTheme();

            // Eliminar duplicado de validadores de Avalonia
            if (BindingPlugins.DataValidators.Count > 0)
                BindingPlugins.DataValidators.RemoveAt(0);

            // Inicializar ventana principal
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(),
            };
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
}
