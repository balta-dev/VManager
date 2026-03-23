using System;
using Avalonia.Controls;
using VManager.Behaviours.X11DragDrop;

namespace VManager.Views.Herramientas;

public partial class Herramienta3View : SoundEnabledUserControl
{
    private X11DragFeedbackApplier? _feedbackApplier;

    public Herramienta3View()
    {
        InitializeComponent();

        var border = this.FindControl<Border>("DropZoneBorder");
        if (border != null && OperatingSystem.IsLinux())
        {
            _feedbackApplier = new X11DragFeedbackApplier(border);
        }
    }
}