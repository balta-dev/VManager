using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia.Controls;
using VManager.ViewModels;

namespace VManager.Controls
{
    public class FluidWrapController : IDisposable
    {
        private readonly Canvas _mainCanvas;
        private readonly Canvas _videoBlock;
        private readonly Canvas _audioBlock;
        private readonly Canvas _progressBar;
        private readonly Canvas _convertButton;
        private readonly Canvas _statusLabel;
        private readonly Canvas _fileDisplay;
        private readonly Herramienta3ViewModel _viewModel;
        private readonly Dictionary<Control, double> _originalTops = new();

        /// <summary>
        /// Configuraciones estáticas para el diseño fluido.
        /// </summary>
        private static class LayoutConfig
        {
            public const double HorizontalSpacing = 124;
            public const double VerticalSpacing = 10;
            public const double ThresholdWidth = 440;
            public const double OffsetX = -44;
            public const double OffsetY = -140;
            public const double MovableOffset = 80;
            public const double BaseY = 150;
            public const double CharacterWidthEstimate = 7.5;
        }
        
        /// Inicializa una nueva instancia del controlador
        /// 
        /// <param name="mainCanvas">El lienzo principal que contiene los controles.</param>
        /// <param name="videoBlock">El bloque de video.</param>
        /// <param name="audioBlock">El bloque de audio.</param>
        /// <param name="progressBar">La barra de progreso.</param>
        /// <param name="convertButton">El botón de conversión.</param>
        /// <param name="statusLabel">La etiqueta de estado.</param>
        /// <param name="fileDisplay">El control para mostrar el archivo.</param>
        /// <param name="viewModel">El ViewModel asociado.</param>
        public FluidWrapController(
            Canvas mainCanvas,
            Canvas videoBlock,
            Canvas audioBlock,
            Canvas progressBar,
            Canvas convertButton,
            Canvas statusLabel,
            Canvas fileDisplay,
            Herramienta3ViewModel viewModel)
        {
            _mainCanvas = mainCanvas ?? throw new ArgumentNullException(nameof(mainCanvas));
            _videoBlock = videoBlock ?? throw new ArgumentNullException(nameof(videoBlock));
            _audioBlock = audioBlock ?? throw new ArgumentNullException(nameof(audioBlock));
            _progressBar = progressBar ?? throw new ArgumentNullException(nameof(progressBar));
            _convertButton = convertButton ?? throw new ArgumentNullException(nameof(convertButton));
            _statusLabel = statusLabel ?? throw new ArgumentNullException(nameof(statusLabel));
            _fileDisplay = fileDisplay ?? throw new ArgumentNullException(nameof(fileDisplay));
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));

            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            InitializePositions();
        }

        
        /// Libera los recursos utilizados por el controlador.
        
        public void Dispose()
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            GC.SuppressFinalize(this);
        }

        
        /// Maneja los cambios en las propiedades del ViewModel y actualiza las posiciones si es necesario.
        
        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(_viewModel.Status) or
                nameof(_viewModel.OutputPath) or
                nameof(_viewModel.Warning))
            {
                UpdateControlPositions();
            }
        }

        
        /// Almacena las posiciones iniciales de los controles en el lienzo.
        
        private void InitializePositions()
        {
            var controls = new[] { _progressBar, _convertButton, _statusLabel, _fileDisplay };
            foreach (var control in controls)
            {
                double top = Canvas.GetTop(control);
                if (double.IsNaN(top))
                {
                    top = LayoutConfig.BaseY;
                }
                _originalTops[control] = top;
                Canvas.SetTop(control, top);

                double left = CalculateInitialLeft(control);
                Canvas.SetLeft(control, AdjustHorizontalCenter(control, left));
            }
        }

        
        /// Calcula la posición horizontal inicial de un control.
        
        private double CalculateInitialLeft(Control control)
        {
            if (control == _statusLabel)
            {
                return (_mainCanvas.Bounds.Width - GetEstimatedTextWidth()) / 2;
            }
            return (_mainCanvas.Bounds.Width - control.Bounds.Width) / 2;
        }

        
        /// Ajusta la posición horizontal de un control según su tipo.
        
        private double AdjustHorizontalCenter(Control control, double baseLeft)
        {
            return control switch
            {
                _ when control == _progressBar => baseLeft - 250,
                _ when control == _convertButton => baseLeft - 55,
                _ when control == _statusLabel => baseLeft + 10,
                _ when control == _fileDisplay => baseLeft - 25,
                _ => baseLeft
            };
        }

      
        /// Actualiza las posiciones de los bloques de video y audio según el ancho del lienzo.
        
        public void UpdateCodecsBlocksPosition()
        {
            double canvasWidth = _mainCanvas.Bounds.Width;
            double videoWidth = _videoBlock.Bounds.Width;
            double audioWidth = _audioBlock.Bounds.Width;
            bool isSmallScreen = canvasWidth < LayoutConfig.ThresholdWidth;

            _viewModel.HeightBlock = isSmallScreen ? 370 : 300;

            double startX = (canvasWidth - videoWidth) / 2 + LayoutConfig.OffsetX;
            double startY = LayoutConfig.BaseY + LayoutConfig.OffsetY;

            if (!isSmallScreen)
            {
                // Disposición horizontal: bloques de video y audio lado a lado
                startX = (canvasWidth - (videoWidth + LayoutConfig.HorizontalSpacing + audioWidth)) / 2 + LayoutConfig.OffsetX;
                Canvas.SetLeft(_videoBlock, startX);
                Canvas.SetTop(_videoBlock, startY);
                Canvas.SetLeft(_audioBlock, startX + videoWidth + LayoutConfig.HorizontalSpacing);
                Canvas.SetTop(_audioBlock, startY);
            }
            else
            {
                // Disposición vertical: audio debajo de video
                Canvas.SetLeft(_videoBlock, startX);
                Canvas.SetTop(_videoBlock, startY);
                Canvas.SetLeft(_audioBlock, startX);
                Canvas.SetTop(_audioBlock, startY + _videoBlock.Bounds.Height + LayoutConfig.VerticalSpacing);
            }
        }

     
        /// Actualiza las posiciones de los controles según el tamaño del lienzo.
        
        public void UpdateControlPositions()
        {
            double canvasWidth = _mainCanvas.Bounds.Width;
            bool isSmallScreen = canvasWidth < LayoutConfig.ThresholdWidth;
            _viewModel.GridWidth = canvasWidth;

            var controls = new[] { _progressBar, _convertButton, _statusLabel, _fileDisplay };
            foreach (var control in controls)
            {
                double top = _originalTops[control] + (isSmallScreen ? LayoutConfig.MovableOffset : 0);
                Canvas.SetTop(control, top);

                if (control == _progressBar)
                {
                    Canvas.SetLeft(control, 0); // Grid se centra automáticamente
                }
                else
                {
                    double left = control == _statusLabel
                        ? (_mainCanvas.Bounds.Width - GetEstimatedTextWidth()) / 2
                        : (canvasWidth - control.Bounds.Width) / 2;
                    Canvas.SetLeft(control, AdjustHorizontalCenter(control, left));
                }
            }
        }

        /// <summary>
        /// Calcula el ancho estimado del texto más largo del ViewModel.
        /// </summary>
        private double GetEstimatedTextWidth()
        {
            int maxLength = Math.Max(
                Math.Max((_viewModel.Status ?? "").Length, (_viewModel.OutputPath ?? "").Length),
                (_viewModel.Warning ?? "").Length);
            return maxLength * LayoutConfig.CharacterWidthEstimate;
        }
    }
}
