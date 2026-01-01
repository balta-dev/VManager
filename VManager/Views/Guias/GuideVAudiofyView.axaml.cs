using Avalonia.Controls;
using VManager.ViewModels;

namespace VManager.Views.Guias
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