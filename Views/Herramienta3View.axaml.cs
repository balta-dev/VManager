using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using System;
using VManager.Controls;
using VManager.ViewModels; // Asegúrate de que incluya ICodecViewModel y CodecViewModelBase

namespace VManager.Views;
public partial class Herramienta3View : SoundEnabledUserControl
{
    private FluidWrapController _fluidController;

    public Herramienta3View()
    {
        InitializeComponent();
        //DataContext = new Herramienta3ViewModel();

        // Se asegura de que los Canvas existan antes de inicializar el controlador
        this.AttachedToVisualTree += Herramienta3View_AttachedToVisualTree;

        // Suscribirse a cambios de tamaño
        this.GetObservable(BoundsProperty).Subscribe(OnBoundsChanged);
    }
    
    private void Herramienta3View_AttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        _fluidController = new FluidWrapController(
            MyCanvas,                // mainCanvas
            VideoBlockCanvas,        // videoBlock
            AudioBlockCanvas,        // audioBlock
            BarraProgreso,           // progressBar
            Convertir,               // convertButton
            Cancelar,               // cancelButton
            Estado,                  // statusLabel (asegúrate de que tenga Name="StatusLabel" en XAML si usas bindings)
            MostrarArchivo,          // fileDisplay
            (ICodecViewModel)DataContext  // Cast a ICodecViewModel (Herramienta3ViewModel lo implementa)
        );

        // Reposicionar al cargar
        _fluidController.UpdateCodecsBlocksPosition();
        _fluidController.UpdateControlPositions();
    }
    private void OnBoundsChanged(Rect bounds)
    {
        if (_fluidController == null)
            return;

        _fluidController.UpdateCodecsBlocksPosition();
        _fluidController.UpdateControlPositions();
    }
}