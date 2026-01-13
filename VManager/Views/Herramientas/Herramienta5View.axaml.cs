using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using VManager.ViewModels;
using VManager.ViewModels.Herramientas;

namespace VManager.Views.Herramientas
{
    public partial class Herramienta5View : SoundEnabledUserControl
    {
        public Herramienta5View()
        {
            InitializeComponent();
            
            // Encontramos el ListBox por nombre
            var listBox = this.FindControl<ListBox>("VideoListBox");

            if (listBox != null)
            {
                // Interceptamos el evento de rueda del mouse
                listBox.PointerWheelChanged += ListBox_PointerWheelChanged;
            }
            
        }
        
        private void ListBox_PointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (sender is not ListBox listBox) return;

            // Obtenemos el ScrollViewer interno del ListBox
            var scrollViewer = listBox.Scroll as ScrollViewer;

            if (scrollViewer == null)
            {
                // Si no hay ScrollViewer (lista corta, no scrolleable), no consumimos
                e.Handled = false;
                return;
            }

            // Chequeamos si hay contenido scrolleable (extent > viewport)
            bool hasScrollableContent = scrollViewer.Extent.Height > scrollViewer.Viewport.Height + 1; // +1 para flotantes

            if (!hasScrollableContent)
            {
                // No hay scrollbar → no consumimos, pasa al padre
                e.Handled = false;
                return;
            }

            // Hay scrollbar → consumimos siempre para que no escape al padre
            e.Handled = true;
        }
        
        private void DownloadHelpBorder_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (DataContext is Herramienta5ViewModel vm)
            {
                vm.ShowDownloadHelp = false;
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}