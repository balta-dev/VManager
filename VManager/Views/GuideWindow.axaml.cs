using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using VManager.ViewModels;

namespace VManager.Views
{
    public partial class GuideWindow : Window
    {
        public GuideWindow(Control guideContent)
        {
            InitializeComponent();
            ContentArea.Content = guideContent;
            DataContext = new AcercaDeViewModel();
        }
    }
}