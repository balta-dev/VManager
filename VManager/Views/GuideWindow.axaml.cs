using Avalonia.Controls;
using VManager.ViewModels;

namespace VManager.Views
{
    public partial class GuideWindow : Window
    {
        private static GuideWindow? _instance;
        public static void ShowGuide(Control content, Window owner)
        {
            if (_instance == null || _instance.IsClosed)
            {
                _instance = new GuideWindow(content)
                {
                    Owner = owner
                };

                _instance.Closed += (_, _) => _instance = null;
                _instance.Show();
            }
            else
            {
                _instance.ContentArea.Content = content;
                _instance.Activate();
            }
        }
        private bool IsClosed { get; set; }
        public GuideWindow(Control guideContent)
        {
            InitializeComponent();
            ContentArea.Content = guideContent;
            DataContext = new AcercaDeViewModel();
            
            Closed += (_, _) => IsClosed = true;
        }
    }
}