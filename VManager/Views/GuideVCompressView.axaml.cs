using Avalonia.Controls;
using VManager.ViewModels;

namespace VManager.Views
{
    public partial class GuideVCompressView : UserControl
    {
        public GuideVCompressView()
        {
            InitializeComponent();
            DataContext = new AcercaDeViewModel();
        }
        
    }
}