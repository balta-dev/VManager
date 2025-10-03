using Avalonia.Controls;
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
    }
}