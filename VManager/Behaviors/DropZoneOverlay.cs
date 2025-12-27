using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Input;
using Avalonia.Threading;
using System;
using System.Threading.Tasks;

namespace VManager.Behaviors
{
    public class DropZoneOverlay
    {
        private Border? _overlayBorder;
        private Border? _targetBorder;
        private Panel? _parentPanel;
        private bool _isActive = false;
        
        public bool IsActive => _isActive;
        
        public async Task<string?> ShowAsync(Window parentWindow, Border targetBorder)
        {
            if (_isActive) return null;
            _isActive = true;
            
            _targetBorder = targetBorder;
            _parentPanel = targetBorder.Parent as Panel;
            
            if (_parentPanel == null)
            {
                Console.WriteLine("⚠️ El Border debe estar dentro de un Panel");
                return null;
            }
            
            // Crear overlay visual
            CreateOverlay();
            
            // Agregar overlay al panel
            _parentPanel.Children.Add(_overlayBorder!);
            _isActive = true;
            
            // Registrar handler de ESC a nivel de ventana
            parentWindow.KeyDown += OnWindowKeyDown;
            
            try
            {
                // Activar ventana X11
                string? result = null;
                try
                {
                    result = await X11DragDropManager.Instance.ShowAsync(parentWindow);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error en X11 window: {ex.Message}");
                }
                return result;
            }
            finally
            {
                // Cleanup
                parentWindow.KeyDown -= OnWindowKeyDown;
                SafeRemoveOverlay();
            }
        }
        
        private void CreateOverlay()
        {
            if (_targetBorder == null) return;
            
            // Crear un Border que cubra exactamente el área del targetBorder
            _overlayBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(150, 0, 0, 0)), // Semi-transparente oscuro
                CornerRadius = _targetBorder.CornerRadius,
                ZIndex = 9999, // Asegurar que esté encima
                IsHitTestVisible = true // Bloquear interacción
            };
            
            // Contenido del overlay
            var stackPanel = new StackPanel
            {
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Spacing = 20
            };
            
            // Ícono de archivo (puedes usar uno de lucide-react o similar)
            var icon = new PathIcon
            {
                Width = 64,
                Height = 64,
                Foreground = Brushes.White,
                Data = Geometry.Parse(
                    "M20 9.50195V8.74985C20 7.50721 18.9926 6.49985 17.75 6.49985H12.0247L9.64368 4.51995C9.23959 4.18393 8.73063 3.99997 8.20509 3.99997H4.24957C3.00724 3.99997 2 5.00686 1.99957 6.24919L1.99561 17.7492C1.99518 18.9921 3.00266 20 4.24561 20H4.27196C4.27607 20 4.28019 20 4.28431 20H18.4693C19.2723 20 19.9723 19.4535 20.167 18.6745L21.9169 11.6765C22.1931 10.5719 21.3577 9.50195 20.2192 9.50195H20ZM4.24957 5.49997H8.20509C8.38027 5.49997 8.54993 5.56129 8.68462 5.6733L11.2741 7.82652C11.4088 7.93852 11.5784 7.99985 11.7536 7.99985H17.75C18.1642 7.99985 18.5 8.33563 18.5 8.74985V9.50195H6.42385C5.39136 9.50195 4.49137 10.2047 4.241 11.2064L3.49684 14.1837L3.49957 6.24971C3.49971 5.8356 3.83546 5.49997 4.24957 5.49997ZM5.69623 11.5701C5.77969 11.2362 6.07969 11.002 6.42385 11.002H20.2192C20.3819 11.002 20.5012 11.1548 20.4617 11.3126L18.7119 18.3107C18.684 18.4219 18.584 18.5 18.4693 18.5H4.28431C4.12167 18.5 4.00233 18.3472 4.04177 18.1894L5.69623 11.5701Z"
                ),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            
            // Texto principal
            var mainText = new TextBlock
            {
                Text = "Arrastra tu archivo aquí",
                FontSize = 24,
                FontWeight = FontWeight.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
            };
            
            // Texto secundario
            var subText = new TextBlock
            {
                Text = "o presiona ESC para cancelar",
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(200, 200, 200)),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Opacity = 0.8
            };
            
            stackPanel.Children.Add(icon);
            stackPanel.Children.Add(mainText);
            stackPanel.Children.Add(subText);

            var innerBox = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(220, 30, 30, 30)),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(32),
                Child = stackPanel,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };

            _overlayBorder.Child = innerBox;
            
            // Copiar el layout del target border
            if (_targetBorder.GetValue(Grid.RowProperty) is int row)
                _overlayBorder.SetValue(Grid.RowProperty, row);
            
            if (_targetBorder.GetValue(Grid.ColumnProperty) is int col)
                _overlayBorder.SetValue(Grid.ColumnProperty, col);
            
            if (_targetBorder.GetValue(Grid.RowSpanProperty) is int rowSpan)
                _overlayBorder.SetValue(Grid.RowSpanProperty, rowSpan);
            
            if (_targetBorder.GetValue(Grid.ColumnSpanProperty) is int colSpan)
                _overlayBorder.SetValue(Grid.ColumnSpanProperty, colSpan);
            
            _overlayBorder.Margin = _targetBorder.Margin;
            _overlayBorder.Padding = _targetBorder.Padding;
        }
        
        private void OnWindowKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                X11DragDropManager.Instance.ForceClose();
                SafeRemoveOverlay();
                _isActive = false;
                e.Handled = true;
            }
        }
        
        private void SafeRemoveOverlay()
        {
            if (_overlayBorder?.Parent is Panel panel)
            {
                panel.Children.Remove(_overlayBorder);
            }
            _overlayBorder = null;
            _isActive = false;
        }
        
        public void ForceClose()
        {
            if (!_isActive) return;
            Console.WriteLine("Forzando cierre de ventana X11...");
            SafeRemoveOverlay();
            X11DragDropManager.Instance.ForceClose();
            _isActive = false;
        }
    }
}