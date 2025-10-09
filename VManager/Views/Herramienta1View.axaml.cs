using Avalonia.Markup.Xaml;

namespace VManager.Views
{
    public partial class Herramienta1View : SoundEnabledUserControl
    {
        public Herramienta1View()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}