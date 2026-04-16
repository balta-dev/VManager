using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using VManager.ViewModels;

namespace VManager.Views
{
    public partial class ConfigurationView : SoundEnabledUserControl
    {
        public ConfigurationView()
        {
            InitializeComponent();

            this.DataContextChanged += (_, _) =>
            {
                if (DataContext is ConfigurationViewModel vm)
                {
                    vm.RequestScrollToBottom = () =>
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            var scroll = this.FindControl<ScrollViewer>("MainScroll");
                            scroll?.ScrollToEnd();
                        }, Avalonia.Threading.DispatcherPriority.Loaded);
                    };
                }
            };
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
        
    }
}