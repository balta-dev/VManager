using Avalonia;
using System;
using VManager.Controls;
using VManager.ViewModels; // Asegurarse de que incluya ICodecViewModel y CodecViewModelBase

namespace VManager.Views;

public partial class Herramienta3View : SoundEnabledUserControl
{
    private FluidWrapController _fluidController = null!; // Garantiza inicialización antes de uso

    public Herramienta3View()
    {
        InitializeComponent();

        // Se asegura de que los Canvas existan antes de inicializar el controlador
        this.AttachedToVisualTree += Herramienta3View_AttachedToVisualTree;

        // Suscribirse a cambios de tamaño
        this.GetObservable(BoundsProperty).Subscribe(OnBoundsChanged);
    }

    private void Herramienta3View_AttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        var viewModel = DataContext as ICodecViewModel;
        if (viewModel == null)
            return; // O lanzar excepción si DataContext es obligatorio

        _fluidController = new FluidWrapController(
            MyCanvas,                // mainCanvas
            VideoBlockCanvas,        // videoBlock
            AudioBlockCanvas,        // audioBlock
            BarraProgreso,           // progressBar
            Convertir,               // convertButton
            Cancelar,                // cancelButton
            Estado,                  // statusLabel
            MostrarArchivo,          // fileDisplay
            viewModel                // viewModel seguro
        );

        // Reposicionar al cargar
        _fluidController.UpdateCodecsBlocksPosition();
        _fluidController.UpdateControlPositions();
    }

    private void OnBoundsChanged(Rect bounds)
    {
        _fluidController?.UpdateCodecsBlocksPosition();
        _fluidController?.UpdateControlPositions();
    }
}