using Avalonia.Markup.Xaml;

namespace VManager.Views
{
    public partial class AcercaDeView : SoundEnabledUserControl
    {
        public AcercaDeView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
        
    }
}