using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using VManager.Services;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Interactivity;
using VManager.Behaviors;
using VManager.Controls;
using VManager.ViewModels;

namespace VManager.Views;

    public partial class Herramienta2View : SoundEnabledUserControl
    {
        private FluidWrapController _fluidController;
        public Herramienta2View()
        {
            InitializeComponent();

            this.AttachedToVisualTree += (s, e) =>
            {
                // Inicializa el controlador
                _fluidController = new FluidWrapController(
                    MyCanvas,
                    VideoBlockCanvas,
                    AudioBlockCanvas,
                    BarraProgreso,
                    Comprimir,
                    Cancelar,
                    Estado,
                    MostrarArchivo,
                    (ICodecViewModel)DataContext
                );

                // Suscribirse a cambios de tamaño
                this.GetObservable(BoundsProperty).Subscribe(new Action<Rect>(OnBoundsChanged));

                // Reposicionar bloques al cargar
                _fluidController.UpdateCodecsBlocksPosition();
                _fluidController.UpdateControlPositions();
            };
        }
    
        private void Herramienta2View_AttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            _fluidController = new FluidWrapController(
                MyCanvas,                // mainCanvas
                VideoBlockCanvas,        // videoBlock
                AudioBlockCanvas,        // audioBlock
                BarraProgreso,           // progressBar
                Comprimir,               // convertButton
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
