using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using VManager.ViewModels;

namespace VManager.Views
{
    public partial class GuideVAudiofyView : UserControl
    {
        public GuideVAudiofyView()
        {
            InitializeComponent();
            DataContext = new AcercaDeViewModel();
        }
        
    }
}