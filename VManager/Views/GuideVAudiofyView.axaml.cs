using Avalonia.Controls;
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