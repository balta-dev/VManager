using System;
using System.IO;
using System.Reactive.Linq;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;

namespace Updater
{
    public partial class UpdateWindow : Window
    {
        
        private static readonly string ConfigPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "VManager",
            "config.json");
       
        private Color? _customColor;
        
        public UpdateWindow()
        {
            InitializeComponent();
            _customColor = LoadCustomColorFromConfig();
            Console.WriteLine(
                Application.Current!.Resources["PanelBackgroundBrush"]
            );
            
            Title = App.UpdaterLocalization.T("WindowTitle");

            this.FindControl<TextBlock>("AwaitUpdate")!.Text =
                App.UpdaterLocalization.T("CheckingUpdates");
            
            var accentObs = this.GetResourceObservable("SystemAccentColor")!
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
        }
        
        private Color? LoadCustomColorFromConfig()
        {
            Console.WriteLine("[Updater] Entrando a LoadCustomColorFromConfig()");

            if (!File.Exists(ConfigPath))
            {
                Console.WriteLine("[Updater] Config no existe");
                return null;
            }

            try
            {
                var json = File.ReadAllText(ConfigPath);
                Console.WriteLine("[Updater] JSON leído OK");

                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                if (root.TryGetProperty("SelectedColor", out var selectedColorProp) ||
                    root.TryGetProperty("selectedColor", out selectedColorProp) ||
                    root.TryGetProperty("SELECTEDCOLOR", out selectedColorProp))
                {
                    string hex = selectedColorProp.GetString() ?? "";

                    if (string.IsNullOrWhiteSpace(hex))
                    {
                        Console.WriteLine("[Updater] SelectedColor vacío en JSON");
                        return null;
                    }

                    string hexUpper = hex.Trim().ToUpperInvariant();
                    Console.WriteLine($"[Updater] HEX encontrado y normalizado: {hexUpper}");

                    if (Color.TryParse(hexUpper, out var color))
                    {
                        Console.WriteLine($"[Updater] COLOR CARGADO PERFECTO: {color}");
                        return color;
                    }
                    else
                    {
                        Console.WriteLine("[Updater] TryParse falló");
                        return null;
                    }
                }
                else
                {
                    Console.WriteLine("[Updater] Propiedad 'SelectedColor' NO encontrada en JSON");
                    return null;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Updater] Error: {ex.Message}");
                return null;
            }
        }
        
        private void ApplyCustomAccent(ThemeVariant? theme = null)
        {
            var actualTheme = theme ?? ActualThemeVariant;
            var accent = GetSystemAccentColor();

            var adjustedAccent = AdjustColorForAccentTheme(accent, actualTheme);
            var foreground = GetContrastingColor(adjustedAccent);
            Application.Current!.Resources["AccentBrush"] = new SolidColorBrush(adjustedAccent);
            Application.Current.Resources["AccentForegroundBrush"] = new SolidColorBrush(foreground);

            var redButton = Color.FromArgb(0xFF, 0xBF, 0x24, 0x24);
            var adjustedRed = AdjustColorForAccentTheme(redButton, actualTheme);
            Application.Current.Resources["RedButtonBrush"] = new SolidColorBrush(adjustedRed);
            var redForeground = GetContrastingColor(adjustedRed);
            Application.Current.Resources["RedButtonForegroundBrush"] = new SolidColorBrush(redForeground);
        }
        private Color GetSystemAccentColor()
        {
            // 1. Prioridad: color personalizado del config (solo lectura)
            if (_customColor.HasValue)
                return _customColor.Value;

            // 2. Si no hay custom → color del sistema
            if (Application.Current!.TryGetResource("SystemAccentColor", ActualThemeVariant, out var value) && value is Color systemAccent)
                return systemAccent;

            // Fallback raro
            return Colors.CornflowerBlue;
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
            if ((theme == ThemeVariant.Light && luminance < luminanceThreshold) ||
                (theme == ThemeVariant.Dark && luminance > 1 - luminanceThreshold))
            {
                adjusted = theme == ThemeVariant.Dark ? DarkenColor(adjusted, 0.3) : LightenColor(adjusted, 0.3);
            }

            return adjusted;
        }
        
        private Color DarkenColor(Color color, double factor)
        {
            byte Adjust(byte c) => (byte)Math.Clamp(c * (1 - factor), 0, 255);
            return new Color(color.A, Adjust(color.R), Adjust(color.G), Adjust(color.B));
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}