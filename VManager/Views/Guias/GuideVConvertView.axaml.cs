using Avalonia.Controls;
using VManager.ViewModels;

namespace VManager.Views.Guias
{
    public partial class GuideVConvertView : UserControl
    {
        public GuideVConvertView()
        {
            InitializeComponent();
            DataContext = new AcercaDeViewModel();
        }
        
    }
}