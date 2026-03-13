using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace VManager.Views
{
    public partial class ErrorWindow : Window
    {
        public ErrorWindow(string message, string? title, string? color)
        {
            InitializeComponent();
            DataContext = this;

            MessageText.Text = message;
            TitleText.Text = title ?? "Error";

            var hex = color ?? "#FFFF6347";
            TitleText.Foreground = SolidColorBrush.Parse(hex);
        }

        private void CloseWindow(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}