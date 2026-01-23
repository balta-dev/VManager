using Avalonia.Controls;
using Avalonia.Interactivity;

namespace VManager.Views
{
    public partial class ErrorWindow : Window
    {
        public ErrorWindow(string message)
        {
            InitializeComponent();
            MessageText.Text = message;
        }

        private void CloseWindow(object? sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}