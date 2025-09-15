using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using VManager.Services;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using VManager.Behaviors;

namespace VManager.Views
{
    public partial class Herramienta3View : SoundEnabledUserControl
    {
        public Herramienta3View()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
        
    }
}