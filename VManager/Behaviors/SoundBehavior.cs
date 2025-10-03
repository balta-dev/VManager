using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using VManager.Services;
using Avalonia.VisualTree;
using Avalonia.Controls;

namespace VManager.Behaviors
{
    public static class SoundBehavior
    {
        public static void Attach(ILogical container)
        {
            var buttons = container.GetLogicalDescendants().OfType<Button>().ToList();

            foreach (var button in buttons)
            {
                // Desuscribimos por las dudas
                button.Click -= OnButtonClick;

                // Suscribimos el mismo handler para todos los botones
                button.Click += OnButtonClick;
            }
            

            if (container is Control ctrl)
            {
                var window = ctrl.GetVisualRoot() as Window;
                if (window != null)
                {
                    window.AddHandler(InputElement.KeyDownEvent, async (s, e) =>
                    {
                        var buttons = container.GetLogicalDescendants().OfType<Button>();
                        foreach (var button in buttons)
                        {
                            if (button.HotKey?.Matches(e) == true)
                            {
                                await PlayButtonSound(button);
                                break;
                            }
                        }
                    }, handledEventsToo: true);
                }
            }

        }

        private static async void OnButtonClick(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                await PlayButtonSound(button);
            }
        }

        private static async Task PlayButtonSound(Button button)
        {
            switch (button.Name)
            {
                case "ToggleTheme":
                    await SoundManager.Play("toggletheme.wav");
                    break;

                case "ClearInfo":
                    await SoundManager.Play("click.wav");
                    break;
                
                default:
                    if (button.Name != null && button.Name.StartsWith("QuestionMark"))
                    {
                        await SoundManager.Play("dummy.wav");
                    }
                    else
                    {
                        await SoundManager.Play("click.wav");
                    }
                    break;
            }
        }
    }

}