using System;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using VManager.Behaviors;

namespace VManager.Views
{
    public partial class Herramienta1View : SoundEnabledUserControl
    {
        private X11DragFeedbackApplier? _feedbackApplier;
        public Herramienta1View()
        {
            InitializeComponent();
            
            var border = this.FindControl<Border>("DropZoneBorder");
            if (border != null && OperatingSystem.IsLinux())
            {
                _feedbackApplier = new X11DragFeedbackApplier(border);
            }
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
        
    }
}