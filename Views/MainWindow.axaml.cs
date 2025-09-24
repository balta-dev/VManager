using System;
using Avalonia.Controls;
using Avalonia.Input;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using VManager.Behaviors;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using ReactiveUI;
using VManager.Services;
using VManager.ViewModels;

namespace VManager.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            SoundBehavior.Attach(this);
            SoundManager.Play("dummy.wav");
            
            var accentObs = this.GetResourceObservable("SystemAccentColor")
                .OfType<Color>()
                .DistinctUntilChanged()
                .Where(c => c != Color.FromArgb(0xFF, 0x00, 0x78, 0xD7)); //default de systemaccent antes de tomarlo del resource padre

            var themeObs = this.GetObservable(ActualThemeVariantProperty)
                .Select(_ => default(Color?)); // no aporta color, solo trigger

            Observable.Merge(accentObs.Select(c => (Color?)c), themeObs)
                .Subscribe(_ =>
                {
                    ApplyCustomAccent();
                });
            
            this.Closing += MainWindow_Closing;
        }
        
        private bool _allowClose = false;

        private async void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
        {
            if (_allowClose)
                return; // Permitir el cierre

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = desktop.MainWindow;
                if (mainWindow?.DataContext is MainWindowViewModel mainVM && mainVM.CurrentView is ViewModelBase current)
                {
                    if (current.IsOperationRunning)
                    {
                        e.Cancel = true; // Cancelamos el cierre inicialmente
                
                        // Esperamos el resultado del diálogo
                        bool shouldClose = await current.ShowCancelDialogInMainWindow(fromWindowClose: true);
                
                        if (shouldClose)
                        {
                            _allowClose = true;
                            this.Close(); // Cerrar la ventana
                        }
                        // Si no should close, simplemente no hacemos nada y la ventana queda abierta
                    }
                }
            }
        }
        
        private void ApplyCustomAccent(ThemeVariant? theme = null)
        {
            var actualTheme = theme ?? ActualThemeVariant;
            var accent = GetSystemAccentColor();
            var background = (actualTheme == ThemeVariant.Dark) ? Colors.Black : Colors.White;
            var adjustedAccent = AdjustColorForAccentTheme(accent, actualTheme);
            var foreground = GetContrastingColor(adjustedAccent);
            Application.Current.Resources["AccentBrush"] = new SolidColorBrush(adjustedAccent);
            Application.Current.Resources["AccentForegroundBrush"] = new SolidColorBrush(foreground);

            var redButton = Color.FromArgb(0xFF, 0xBF, 0x24, 0x24);
            var adjustedRed = AdjustColorForAccentTheme(redButton, actualTheme);
            Application.Current.Resources["RedButtonBrush"] = new SolidColorBrush(adjustedRed);
            var redForeground = GetContrastingColor(adjustedRed);
            Application.Current.Resources["RedButtonForegroundBrush"] = new SolidColorBrush(redForeground);
        }
        private Color GetSystemAccentColor()
        {
            if (Application.Current.TryGetResource("SystemAccentColor", null, out var value) && value is Color accent) 
                return accent;
            
            return Color.FromArgb(0xFF, 0xFF, 0x00, 0x00); // acá es IMPOSIBLE que llegue
         
        }
        
        private double GetRelativeLuminance(Color color)
        {
            double R = color.R / 255.0;
            double G = color.G / 255.0;
            double B = color.B / 255.0;

            R = (R <= 0.03928) ? R / 12.92 : Math.Pow((R + 0.055) / 1.055, 2.4);
            G = (G <= 0.03928) ? G / 12.92 : Math.Pow((G + 0.055) / 1.055, 2.4);
            B = (B <= 0.03928) ? B / 12.92 : Math.Pow((B + 0.055) / 1.055, 2.4);

            return 0.2126 * R + 0.7152 * G + 0.0722 * B;
        }

        private double GetContrastRatio(Color c1, Color c2)
        {
            var l1 = GetRelativeLuminance(c1);
            var l2 = GetRelativeLuminance(c2);
            if (l1 < l2) (l1, l2) = (l2, l1);
            return (l1 + 0.05) / (l2 + 0.05);
        }
        private Color LightenColor(Color color, double factor)
        {
            byte Adjust(byte c) => (byte)Math.Clamp(c + (255 - c) * factor, 0, 255);
            return new Color(color.A, Adjust(color.R), Adjust(color.G), Adjust(color.B));
        }
        
        private Color GetContrastingColor(Color background, double minContrast = 4.5)
        {
            var white = Colors.White;
            var black = Colors.Black;

            var contrastWithWhite = GetContrastRatio(background, white);
            var contrastWithBlack = GetContrastRatio(background, black);

            // Si el fondo es muy oscuro, usar blanco; si es muy claro, usar negro
            return (GetRelativeLuminance(background) < 0.5) ? white : black;
        }
        private Color AdjustColorForAccentTheme(Color baseColor, ThemeVariant theme, double minContrast = 4.5)
        {
            Color adjusted = baseColor;

            // Priorizar ajuste según tema
            bool lighten = theme == ThemeVariant.Dark; // Aclarar en modo oscuro, oscurecer en modo claro
            double factor = 0.1; // Paso inicial del 10%
            double luminanceThreshold = 0.2; // Umbral para considerar el color "muy oscuro"

            for (int i = 0; i < 30; i++)
            {
                byte Adjust(byte c) => (byte)Math.Clamp(lighten ? c + (255 - c) * factor : c * (1 - factor), 0, 255);

                adjusted = new Color(adjusted.A,
                    Adjust(adjusted.R),
                    Adjust(adjusted.G),
                    Adjust(adjusted.B));

                // Verificar contraste con el fondo
                double contrast = GetContrastRatio(adjusted, (theme == ThemeVariant.Dark) ? Colors.Black : Colors.White);
                if (contrast >= minContrast)
                    break;

                // Reducir factor si no se alcanza el contraste
                if (i > 10 && contrast < minContrast * 0.9)
                    factor *= 0.8;
            }

            // Si el color ajustado es muy oscuro (luminancia < umbral) y estamos en modo claro, forzar un ajuste adicional
            double luminance = GetRelativeLuminance(adjusted);
            if (theme == ThemeVariant.Light && luminance < luminanceThreshold)
            {
                adjusted = LightenColor(adjusted, 0.3); // Aclarar un 30% si está demasiado oscuro en modo claro
            }

            return adjusted;
        }
        
    }
}