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
                button.PointerEntered -= OnHover;
                button.Click -= OnClick;

                if (button.Name == "ToggleTheme")
                {
                    button.PointerEntered += OnHover;
                    button.Click += (s, e) => SoundManager.Play("toggletheme.wav");
                }
                else
                {
                    button.PointerEntered += OnHover;
                    button.Click += OnClick;
                }
            }
        }

        private static void OnHover(object? sender, PointerEventArgs e)
            => SoundManager.Play("hover.wav");

        private static void OnClick(object? sender, RoutedEventArgs e)
            => SoundManager.Play("click.wav");
    }
}