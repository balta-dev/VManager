using System;
using Avalonia.Controls;
using Avalonia.Input;
using System.Linq;
using VManager.Behaviors;
using Avalonia.Interactivity;
using VManager.Services;

namespace VManager.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            SoundBehavior.Attach(this);
            SoundManager.Play("dummy.wav");
        }
        
    }
}