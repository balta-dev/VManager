using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using System;
using System.IO;
using Avalonia.Layout;
using Avalonia.Platform;

namespace VManager.Views
{
    public class ToastWindow : Window
    {
        private double _opacityIncrement = 0.1;
        private int _fadeIntervalMs = 30;
        private double _slideIncrement = 60; // Pixels to move per tick
        private PixelPoint _startPosition;
        private PixelPoint _finalPosition;

        public ToastWindow(string title, string message, string iconPath, int durationSeconds = 5)
        {
            // Window configuration
            this.Topmost = true;
            this.CanResize = false;
            this.SystemDecorations = SystemDecorations.None;
            this.WindowStartupLocation = WindowStartupLocation.Manual;
            this.Width = 350;
            this.Height = 125;
            this.Opacity = 0;

            // Calculate final and start positions
            var screen = Screens.Primary;
            if (screen != null)
            {
                _finalPosition = new PixelPoint(
                    (int)screen.Bounds.Width - (int)this.Width - 20,
                    (int)screen.Bounds.Height - (int)this.Height - 50);
                _startPosition = new PixelPoint(
                    (int)screen.Bounds.Width,
                    _finalPosition.Y);
            }

            // Set initial position
            this.Position = _startPosition;

            // Rounded border
            var border = new Border
            {
                Background = new SolidColorBrush(Color.Parse("#FF1A1A1A")), // Dark gray Windows style
                Padding = new Thickness(10)
            };

            // Horizontal layout: icon + text
            var horizontalPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 10
            };

            // Icon
            var uri = new Uri("avares://VManager/Assets/VManager.ico");
            using var stream = AssetLoader.Open(uri);
            var icon = new Image
            {
                Source = new Bitmap(stream),
                Width = 16,
                Height = 16,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
            };
            horizontalPanel.Children.Add(icon);
            

            // Text
            var textPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 2
            };

            textPanel.Children.Add(new TextBlock
            {
                Text = "VManager",
                Foreground = Brushes.White,
                FontWeight = Avalonia.Media.FontWeight.Bold,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 13)
            });
            
            textPanel.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = Brushes.White,
                FontWeight = Avalonia.Media.FontWeight.Bold,
                FontSize = 14,
                Margin = new Thickness(0, 0, 0, 2)
            });

            textPanel.Children.Add(new TextBlock
            {
                Text = message,
                Foreground = Brushes.LightGray,
                TextTrimming = TextTrimming.CharacterEllipsis, // Puntos suspensivos al final
                MaxWidth = 300, 
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12
            });

            horizontalPanel.Children.Add(textPanel);
            border.Child = horizontalPanel;
            this.Content = border;

            // Close on click
            this.PointerPressed += (_, __) => FadeOutAndClose();

            // Fade-in and slide-in
            this.Opened += (_, __) => FadeInAndSlideIn();

            // Auto-close after duration (with buffer for animations)
            DispatcherTimer.RunOnce(() => FadeOutAndClose(), TimeSpan.FromSeconds(durationSeconds + 0.5));
        }

        private void FadeInAndSlideIn()
        {
            this.Opacity = 0;
            this.Position = _startPosition;

            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(_fadeIntervalMs)
            };

            timer.Tick += (s, e) =>
            {
                // Incrementar opacidad de forma segura
                this.Opacity = Math.Min(this.Opacity + _opacityIncrement, 1);

                // Mover hacia la posición final de forma segura
                int newX = Math.Max(this.Position.X - (int)_slideIncrement, _finalPosition.X);
                this.Position = new PixelPoint(newX, this.Position.Y);

                // Detener el timer si ambas condiciones se cumplieron
                if (this.Opacity >= 1 && this.Position.X <= _finalPosition.X)
                {
                    timer.Stop();
                }
            };

            timer.Start();
        }

        private void FadeOutAndClose()
        {
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(_fadeIntervalMs)
            };

            timer.Tick += (s, e) =>
            {
                // Reducir opacidad de forma segura
                this.Opacity = Math.Max(this.Opacity - _opacityIncrement, 0);

                // Mover hacia fuera de pantalla de forma segura
                int newX = Math.Min(this.Position.X + (int)_slideIncrement, _startPosition.X);
                this.Position = new PixelPoint(newX, this.Position.Y);

                // Cuando la ventana esté completamente fuera, cerrar
                if (this.Position.X >= _startPosition.X && this.Opacity <= 0)
                {
                    timer.Stop();
                    this.Close();
                }
            };

            timer.Start();
        }

    }
}