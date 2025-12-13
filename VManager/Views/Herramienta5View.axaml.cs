using Avalonia.Input;
using Avalonia.Markup.Xaml;
using VManager.ViewModels;

namespace VManager.Views
{
    public partial class Herramienta5View : SoundEnabledUserControl
    {
        public Herramienta5View()
        {
            InitializeComponent();
            
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