using Avalonia.Controls;
using VManager.ViewModels;

namespace VManager.Views
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