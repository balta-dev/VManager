using Avalonia.Controls;
using VManager.Behaviors;

namespace VManager.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            SoundBehavior.Attach(this);
        }
    }
}