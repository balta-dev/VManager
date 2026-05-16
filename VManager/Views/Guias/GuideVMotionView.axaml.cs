using Avalonia.Controls;
using VManager.ViewModels;

namespace VManager.Views.Guias
{
    public partial class GuideVMotionView : UserControl
    {
        public GuideVMotionView()
        {
            InitializeComponent();
            DataContext = new AcercaDeViewModel();
        }
        
    }
}