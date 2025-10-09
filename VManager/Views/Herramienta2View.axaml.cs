using System;
using Avalonia;
using VManager.Controls;
using VManager.ViewModels;

namespace VManager.Views;

public partial class Herramienta2View : SoundEnabledUserControl
{
    private FluidWrapController _fluidController = null!; // Garantiza inicialización antes de uso

    public Herramienta2View()
    {
        InitializeComponent();

        this.AttachedToVisualTree += Herramienta2View_AttachedToVisualTree;

        // Suscribirse a cambios de tamaño
        this.GetObservable(BoundsProperty).Subscribe(OnBoundsChanged);
    }

    private void Herramienta2View_AttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        var viewModel = DataContext as ICodecViewModel;
        if (viewModel == null)
            return; // o lanzar excepción si DataContext es obligatorio

        _fluidController = new FluidWrapController(
            MyCanvas,
            VideoBlockCanvas,
            AudioBlockCanvas,
            BarraProgreso,
            Comprimir,
            Cancelar,
            Estado,
            MostrarArchivo,
            viewModel
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

