using System;
using System.Diagnostics;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace VManager.Views
{
    public partial class AcercaDeView : SoundEnabledUserControl
    {
        public AcercaDeView()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
        
        private void OpenGitHub(object? sender, PointerPressedEventArgs e)
        {
            OpenUrl("https://github.com/balta-dev/VManager");
        }
        
        private void OpenLinkedIn(object? sender, PointerPressedEventArgs e)
        {
            OpenUrl("https://www.linkedin.com/in/baltafranz/");
        }

        private void OpenInstagram(object? sender, PointerPressedEventArgs e)
        {
            OpenUrl("https://www.instagram.com/baltafranz/");
        }

        private void OpenGithubProfile(object? sender, PointerPressedEventArgs e)
        {
            OpenUrl("https://github.com/balta-dev");
        }

        private static void OpenUrl(string url)
        {
            try
            {
                // .NET Core / .NET 5+ cross-platform way
                var psi = new ProcessStartInfo(url) { UseShellExecute = true };
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                // opcional: manejar error o mostrar notificación
                Console.WriteLine($"No se pudo abrir la URL: {ex.Message}");
            }
        }
        
    }
}