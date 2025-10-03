using System;
using System.Diagnostics;
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
using Avalonia.ReactiveUI;
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
            
            var assembly = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;
            string version = $"{assembly?.Major}.{assembly?.Minor}.{assembly?.Build}";
            VersionText.Text = $"Versión: {version}";
            
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
            
            this.Opened += async (_, _) => await CheckUpdatesAsync();
            this.Closing += MainWindow_Closing;
            this.KeyDown += OnKeyDown;
            MainSplitView.AddHandler(
                InputElement.KeyDownEvent,
                new EventHandler<KeyEventArgs>(MainSplitview_OnPreviewKeyDown),
                RoutingStrategies.Tunnel);
        }
        
        private void MainSplitview_OnPreviewKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && e.KeyModifiers == KeyModifiers.None)
            {
                e.Handled = true; // ❌ Enter queda bloqueado en toda la sidebar
            }
        }
        
        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && e.KeyModifiers.HasFlag(KeyModifiers.Alt))
            {
                if (this.WindowState == WindowState.FullScreen)
                    this.WindowState = WindowState.Normal;
                else
                    this.WindowState = WindowState.FullScreen;
            }
        }
        
        private async Task CheckUpdatesAsync()
        {
            var update = await UpdateChecker.CheckForUpdateAsync();

            if (update != null && update.UpdateAvailable && !string.IsNullOrEmpty(update.DownloadUrl))
            {
                var dialog = new Window
                {
                    Title = "¡Actualización disponible!",
                    Width = 500,
                    Height = 400,
                    Background = new SolidColorBrush(Color.Parse("#FF1E1E1E")),
                    Foreground = new SolidColorBrush(Color.Parse("#FFFAFAFA")),
                    Content = new StackPanel
                    {
                        Margin = new Thickness(10),
                        Children =
                        {
                            new TextBlock
                            {
                                Text = $"Nueva versión {update.LatestVersion} disponible.",
                                Margin = new Thickness(0,0,0,10),
                                Foreground = new SolidColorBrush(Color.Parse("#FFFFE066")),
                                FontSize = 23,
                                FontWeight = FontWeight.Bold
                            },
                            new ScrollViewer
                            {
                                Height = 300,
                                Content = new TextBlock
                                {
                                    Text = update.ReleaseNotes,
                                    Opacity =  0.85,
                                    TextWrapping = Avalonia.Media.TextWrapping.Wrap
                                }
                            },
                            new Button
                            {
                                Content = "Descargar última versión",
                                Classes = {"Accented"},
                                FontSize = 15
                                // Command will be set after dialog is fully initialized
                            }
                        }
                    }
                };

                // Set the button's command after dialog is initialized
                var button = (dialog.Content as StackPanel).Children.OfType<Button>().First();
                button.Command = ReactiveUI.ReactiveCommand.Create(() =>
                {
                    Process.Start(new ProcessStartInfo(update.DownloadUrl) { UseShellExecute = true });
                    dialog.Close(); // Close the dialog after opening the URL
                }, outputScheduler: AvaloniaScheduler.Instance);

                dialog.Show();
            }
        }
        
        
        private bool _allowClose = false;

        private async void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
        {
            if (_allowClose)
                return; // Permitir el cierre

            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var mainWindow = desktop.MainWindow;
                if (mainWindow?.DataContext is MainWindowViewModel mainVM)
                {
                    // Verificar si hay alguna herramienta corriendo
                    var runningTools = mainVM.Tools.Where(t => t.IsOperationRunning).ToList();

                    if (runningTools.Any())
                    {
                        e.Cancel = true; // Cancelamos el cierre inicialmente

                        // Mostrar diálogo de cancelación usando la herramienta activa (CurrentView)
                        if (mainVM.CurrentView is ViewModelBase current)
                        {
                            bool shouldClose = await current.ShowCancelDialogInMainWindow(fromWindowClose: true);

                            if (shouldClose)
                            {
                                // Cancelar TODAS las operaciones en ejecución
                                foreach (var tool in runningTools)
                                {
                                    tool.RequestCancelOperation();
                                }

                                _allowClose = true;
                                this.Close(); // Cerrar la ventana
                            }
                            // Si el usuario cancela el diálogo, la ventana queda abierta
                        }
                    }
                    // Si ninguna herramienta está corriendo, se permite cerrar sin diálogo
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