using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using VManager.Behaviours;
using VManager.Views.Guias;

namespace VManager.Views
{
    public abstract class SoundEnabledUserControl : UserControl
    {
        protected SoundEnabledUserControl()
        {
            this.Loaded += OnLoaded;
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            SoundBehaviour.Attach(this);
        }
        
        protected void OpenGitHub(object? sender, PointerPressedEventArgs e)
        {
            OpenUrl("https://github.com/balta-dev/VManager");
        }
        
        protected void OpenLinkedIn(object? sender, PointerPressedEventArgs e)
        {
            OpenUrl("https://www.linkedin.com/in/baltafranz/");
        }

        protected void OpenInstagram(object? sender, PointerPressedEventArgs e)
        {
            OpenUrl("https://www.instagram.com/baltafranz/");
        }

        protected void OpenGithubProfile(object? sender, PointerPressedEventArgs e)
        {
            OpenUrl("https://github.com/balta-dev");
        }

        protected static void OpenUrl(string url)
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
        
        protected void OpenGuideVCut(object? sender, RoutedEventArgs e)
        {
            var owner = this.GetVisualRoot() as Window;
            GuideWindow.ShowGuide(new GuideVCutView(), owner!);
        }

        protected void OpenGuideVCompress(object? sender, RoutedEventArgs e)
        {
            var owner = this.GetVisualRoot() as Window;
            GuideWindow.ShowGuide(new GuideVCompressView(), owner!);
        }

        protected void OpenGuideVConvert(object? sender, RoutedEventArgs e)
        {
            var owner = this.GetVisualRoot() as Window;
            GuideWindow.ShowGuide(new GuideVConvertView(), owner!);
        }

        protected void OpenGuideVAudiofy(object? sender, RoutedEventArgs e)
        {
            var owner = this.GetVisualRoot() as Window;
            GuideWindow.ShowGuide(new GuideVAudiofyView(), owner!);
        }

        protected void OpenGuideVDownload(object? sender, RoutedEventArgs e)
        {
            var owner = this.GetVisualRoot() as Window;
            GuideWindow.ShowGuide(new GuideVDownloadView(), owner!);
        }
        
    }
}