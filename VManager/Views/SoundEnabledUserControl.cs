using System;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using VManager.Behaviors;

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
            SoundBehavior.Attach(this);
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
            new GuideWindow(new GuideVCutView()).Show();
        }

        protected void OpenGuideVCompress(object? sender, RoutedEventArgs e)
        {
            new GuideWindow(new GuideVCompressView()).Show();
        }

        protected void OpenGuideVConvert(object? sender, RoutedEventArgs e)
        {
            new GuideWindow(new GuideVConvertView()).Show();
        }

        protected void OpenGuideVAudiofy(object? sender, RoutedEventArgs e)
        {
            new GuideWindow(new GuideVAudiofyView()).Show();
        }

        protected void OpenGuideVDownload(object? sender, RoutedEventArgs e)
        {
            new GuideWindow(new GuideVDownloadView()).Show();
        }
        
    }
}