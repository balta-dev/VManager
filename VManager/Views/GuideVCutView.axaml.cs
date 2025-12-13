using Avalonia.Controls;
using Avalonia.Markup.Xaml;
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