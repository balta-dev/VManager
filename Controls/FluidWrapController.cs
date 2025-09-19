using System;
using System.Collections.Generic;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Threading;
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

        private static class LayoutConfig
        {
            public const double HorizontalSpacing = 124;
            public const double VerticalSpacing = 10;
            public const double ThresholdWidth = 440;
            public const double OffsetX = -44;
            public const double OffsetY = -140;
            public const double MovableOffset = 80;
            public const double BaseY = 150;
            public const double CharacterWidthEstimate = 7.45;
        }

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

        public void Dispose()
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            GC.SuppressFinalize(this);
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            Console.WriteLine($"Propiedad cambiada: {e.PropertyName}");
            Dispatcher.UIThread.Post(() =>
            {
                if (e.PropertyName == nameof(_viewModel.VideoPath))
                {
                    UpdateCodecsBlocksPosition(); // Actualiza videoBlock y audioBlock
                    UpdateControlPositions(); // Actualiza statusLabel y otros controles
                }
                else if (e.PropertyName is nameof(_viewModel.Status) or
                         nameof(_viewModel.OutputPath) or
                         nameof(_viewModel.Warning))
                {
                    UpdateControlPositions();
                }
            });
        }

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

        private double CalculateInitialLeft(Control control)
        {
            if (control == _statusLabel)
            {
                return (_mainCanvas.Bounds.Width - GetEstimatedTextWidth()) / 2;
            }
            return (_mainCanvas.Bounds.Width - 
                    (control.DesiredSize.Width > 0 ? control.DesiredSize.Width : control.Bounds.Width)) / 2;
        }

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
                startX = (canvasWidth - (videoWidth + LayoutConfig.HorizontalSpacing + audioWidth)) / 2 + LayoutConfig.OffsetX;
                Canvas.SetLeft(_videoBlock, startX);
                Canvas.SetTop(_videoBlock, startY);
                Canvas.SetLeft(_audioBlock, startX + videoWidth + LayoutConfig.HorizontalSpacing);
                Canvas.SetTop(_audioBlock, startY);
            }
            else
            {
                Canvas.SetLeft(_videoBlock, startX);
                Canvas.SetTop(_videoBlock, startY);
                Canvas.SetLeft(_audioBlock, startX);
                Canvas.SetTop(_audioBlock, startY + _videoBlock.Bounds.Height + LayoutConfig.VerticalSpacing);
            }

            // Invalidar controles individuales
            _videoBlock.InvalidateMeasure();
            _videoBlock.InvalidateArrange();
            _videoBlock.InvalidateVisual();
            _audioBlock.InvalidateMeasure();
            _audioBlock.InvalidateArrange();
            _audioBlock.InvalidateVisual();

            // Invalidar el lienzo
            _mainCanvas.InvalidateMeasure();
            _mainCanvas.InvalidateArrange();
            _mainCanvas.InvalidateVisual();

            // Invalidar el contenedor padre
            if (_mainCanvas.Parent is Control parent)
            {
                parent.InvalidateMeasure();
                parent.InvalidateArrange();
                parent.InvalidateVisual();
            }

            // Forzar actualización retardada
            DispatcherTimer.RunOnce(() =>
            {
                _mainCanvas.InvalidateVisual();
                if (_mainCanvas.Parent is Control parent)
                {
                    parent.InvalidateVisual();
                }
            }, TimeSpan.FromMilliseconds(1));
        }

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
                    Canvas.SetLeft(control, 0);
                }
                else
                {
                    double controlWidth = control.DesiredSize.Width > 0
                        ? control.DesiredSize.Width
                        : control.Bounds.Width;

                    double left = control == _statusLabel
                        ? (_mainCanvas.Bounds.Width - GetEstimatedTextWidth()) / 2
                        : (canvasWidth - controlWidth) / 2;

                    Canvas.SetLeft(control, AdjustHorizontalCenter(control, left));
                }

                control.InvalidateMeasure();
                control.InvalidateArrange();
                control.InvalidateVisual();
            }

            _mainCanvas.InvalidateMeasure();
            _mainCanvas.InvalidateArrange();
            _mainCanvas.InvalidateVisual();

            if (_mainCanvas.Parent is Control parent)
            {
                parent.InvalidateMeasure();
                parent.InvalidateArrange();
                parent.InvalidateVisual();
            }

            // Forzar actualización retardada
            DispatcherTimer.RunOnce(() =>
            {
                _mainCanvas.InvalidateVisual();
                if (_mainCanvas.Parent is Control parent)
                {
                    parent.InvalidateVisual();
                }
            }, TimeSpan.FromMilliseconds(1));
        }

        private double GetEstimatedTextWidth()
        {
            int maxLength = Math.Max(
                Math.Max((_viewModel.Status ?? "").Length, (_viewModel.OutputPath ?? "").Length),
                (_viewModel.Warning ?? "").Length);
            return maxLength * LayoutConfig.CharacterWidthEstimate;
        }
    }
}