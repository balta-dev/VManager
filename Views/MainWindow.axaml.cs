using System;
using Avalonia.Controls;
using Avalonia.Input;
using System.Linq;
using System.Reactive.Linq;
using Avalonia;
using VManager.Behaviors;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using VManager.Services;

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
                .Where(c => c != Color.FromArgb(0xFF, 0x00, 0x78, 0xD7));

            var themeObs = this.GetObservable(ActualThemeVariantProperty)
                .Select(_ => default(Color?)); // no aporta color, solo trigger

            Observable.Merge(accentObs.Select(c => (Color?)c), themeObs)
                .Subscribe(_ =>
                {
                    ApplyCustomAccent();
                });
            
        }
        private void ApplyCustomAccent(ThemeVariant? theme = null)
        {
        
            var actualTheme = theme ?? ActualThemeVariant;
            var accent = GetSystemAccentColor();
            var background = (actualTheme == ThemeVariant.Dark) ? Colors.Black : Colors.White;
            var adjusted = AdjustColorForContrast(accent, background);
            var foreground = GetContrastingColor(adjusted);
            Resources["AccentBrush"] = new SolidColorBrush(adjusted);
            Resources["AccentForegroundBrush"] = new SolidColorBrush(foreground);
            
        }
        private Color GetSystemAccentColor()
        {
            if (Application.Current.TryGetResource("SystemAccentColor", null, out var value) && value is Color accent) 
                return accent;
            
            return Color.FromArgb(0xFF, 0xFF, 0x00, 0x00); // Rojo puro
         
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

        private Color AdjustColorForContrast(Color baseColor, Color background, double minContrast = 4.5)
        {
            if (GetContrastRatio(baseColor, background) >= minContrast)
                return baseColor;

            // Si no cumple, lo aclaro u oscurezco
            var factor = GetRelativeLuminance(baseColor) > GetRelativeLuminance(background) ? -0.3 : 0.3;
            byte Adjust(byte c) => (byte)Math.Clamp(c + (255 * factor), 0, 255);

            return new Color(baseColor.A, Adjust(baseColor.R), Adjust(baseColor.G), Adjust(baseColor.B));
        }
        
        private Color GetContrastingColor(Color background, double minContrast = 4.5)
        {
            var white = Colors.White;
            var black = Colors.Black;

            var contrastWithWhite = GetContrastRatio(background, white);
            var contrastWithBlack = GetContrastRatio(background, black);

            return (contrastWithWhite >= contrastWithBlack) ? white : black;
        }

    }
}