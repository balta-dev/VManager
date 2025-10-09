using Avalonia.Markup.Xaml;

namespace VManager.Views
{
    public partial class ConfigurationView : SoundEnabledUserControl
    {
        public ConfigurationView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
        
    }
}