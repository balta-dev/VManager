using Avalonia.Controls;
using VManager.ViewModels;

namespace VManager.Views.Guias
{
    public partial class GuideVCutView : UserControl
    {
        public GuideVCutView()
        {
            InitializeComponent();
            DataContext = new AcercaDeViewModel();
        }
        
    }
}