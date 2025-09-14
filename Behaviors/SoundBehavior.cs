using System.Linq;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using VManager.Services;

namespace VManager.Behaviors
{
    public static class SoundBehavior
    {
        public static void Attach(ILogical container)
        {
            
            var buttons = container.GetLogicalDescendants().OfType<Button>();

            foreach (var button in buttons)
            {
                button.Click -= OnClick;
                button.Click -= OnToggleThemeClick;
                
                if (button.Name == "ToggleTheme")
                {
                    button.Click += OnToggleThemeClick;
                }
                else
                {
                    button.Click += OnClick;
                }
            }
        }
        
        private static async void OnHover(object? sender, PointerEventArgs e)
        {
            await SoundManager.Play("hover.wav");
        }
        
        private static async void OnClick(object? sender, RoutedEventArgs e)
        {
            await SoundManager.Play("click.wav");
        }
        
        private static async void OnToggleThemeClick(object? sender, RoutedEventArgs e)
        {
            await SoundManager.Play("toggletheme.wav");
        }
    }
}