using Avalonia.Controls;
using Avalonia.Markup.Xaml;
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