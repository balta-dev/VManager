using Avalonia.Controls;

namespace VManager.Views;

public partial class CancelDialog : Window
{
    public CancelDialog()
    {
        InitializeComponent();

        YesButton.Click += (_, _) => Close(true);
        NoButton.Click += (_, _) => Close(false);
    }
    
}