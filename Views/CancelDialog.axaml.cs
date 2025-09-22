using Avalonia.Controls;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

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