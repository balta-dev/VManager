using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using VManager.ViewModels;

namespace VManager.Views
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