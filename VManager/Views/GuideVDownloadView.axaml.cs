using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using VManager.ViewModels;

namespace VManager.Views
{
    public partial class GuideVDownloadView : UserControl
    {
        public GuideVDownloadView()
        {
            InitializeComponent();
            DataContext = new AcercaDeViewModel();
        }
        
        // Abrir la URL para la extensión de Edge
        private void OpenEdgeCookies(object sender, PointerPressedEventArgs e)
        {
            const string url = "https://microsoftedge.microsoft.com/addons/detail/cookies-txt/dilbcaaegopfblcjdjikanigjbcbngbk";  // Aquí va la URL de la extensión de Edge

            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }

        // Similar, podrías agregar otro método para Chrome
        private void OpenChromeCookies(object sender, PointerPressedEventArgs e)
        {
            const string url = "https://chromewebstore.google.com/detail/get-cookiestxt-locally/cclelndahbckbenkjhflpdbgdldlbecc";  // URL de la extensión en Chrome

            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        
    }
}