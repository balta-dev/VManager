using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Threading;
using VManager.ViewModels;

namespace VManager.Controls
{
    public class ControlConfig
    {
        public Control Control { get; set; }
        public double HorizontalOffset { get; set; }
        public bool UseEstimatedWidth { get; set; }
        public double OriginalTop { get; set; }
    }

    public class FluidWrapController : IDisposable
    {
        private readonly Canvas _mainCanvas;
        private readonly ICodecViewModel _viewModel;
        private readonly List<ControlConfig> _mainBlocks;
        private readonly List<ControlConfig> _secondaryControls;
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
            public const double CharacterWidthEstimate = 7.45; // Restaurado de la versión anterior
        }

        public FluidWrapController(
            Canvas mainCanvas,
            Canvas videoBlock,
            Canvas audioBlock,
            Canvas progressBar,
            Canvas convertButton,
            Canvas statusLabel,
            Canvas fileDisplay,
            ICodecViewModel viewModel)
        {
            _mainCanvas = mainCanvas ?? throw new ArgumentNullException(nameof(mainCanvas));
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _mainBlocks = new List<ControlConfig>
            {
                new ControlConfig { Control = videoBlock ?? throw new ArgumentNullException(nameof(videoBlock)) },
                new ControlConfig { Control = audioBlock ?? throw new ArgumentNullException(nameof(audioBlock)) }
            };
            _secondaryControls = new List<ControlConfig>
            {
                new ControlConfig { Control = progressBar ?? throw new ArgumentNullException(nameof(progressBar)), HorizontalOffset = -250 },
                new ControlConfig { Control = convertButton ?? throw new ArgumentNullException(nameof(convertButton)), HorizontalOffset = -55 },
                new ControlConfig { Control = statusLabel ?? throw new ArgumentNullException(nameof(statusLabel)), HorizontalOffset = 10, UseEstimatedWidth = true },
                new ControlConfig { Control = fileDisplay ?? throw new ArgumentNullException(nameof(fileDisplay)), HorizontalOffset = -25 }
            };

            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            //_mainCanvas.LayoutUpdated += (s, e) => Console.WriteLine("Layout actualizado");
            InitializePositions();
        }

        public void Dispose()
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            GC.SuppressFinalize(this);
        }

        private void OnViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            //Console.WriteLine($"Propiedad cambiada: {e.PropertyName}");
            Dispatcher.UIThread.Post(() =>
            {
                if (e.PropertyName == nameof(ICodecViewModel.VideoPath))
                {
                    UpdateCodecsBlocksPosition();
                    UpdateControlPositions();
                }
                else if (e.PropertyName is nameof(ICodecViewModel.Status) or
                         nameof(ICodecViewModel.OutputPath) or
                         nameof(ICodecViewModel.Warning) or
                         nameof(ICodecViewModel.Progress) or
                         nameof(ICodecViewModel.IsFileReadyVisible))
                {
                    UpdateControlPositions();
                }
            });
        }

        private void InitializePositions()
        {
            foreach (var config in _mainBlocks)
            {
                double top = Canvas.GetTop(config.Control);
                if (double.IsNaN(top)) top = LayoutConfig.BaseY;
                config.OriginalTop = top;
                Canvas.SetTop(config.Control, top);
            }

            foreach (var config in _secondaryControls)
            {
                double top = Canvas.GetTop(config.Control);
                if (double.IsNaN(top)) top = LayoutConfig.BaseY;
                _originalTops[config.Control] = top;
                Canvas.SetTop(config.Control, top);

                double left = CalculateInitialLeft(config.Control);
                left = AdjustHorizontalCenter(config.Control, left);
                //Console.WriteLine($"Inicializando {config.Control.Name}: Left={left}, CanvasWidth={_mainCanvas.Bounds.Width}");
                Canvas.SetLeft(config.Control, left);
            }
        }

        private double CalculateInitialLeft(Control control)
        {
            if (control == _secondaryControls.First(c => c.UseEstimatedWidth).Control) // statusLabel
            {
                return (_mainCanvas.Bounds.Width - GetEstimatedTextWidth()) / 2;
            }
            double controlWidth = control.DesiredSize.Width > 0 ? control.DesiredSize.Width : control.Bounds.Width;
            return (_mainCanvas.Bounds.Width - controlWidth) / 2;
        }

        private double AdjustHorizontalCenter(Control control, double baseLeft)
        {
            foreach (var config in _secondaryControls)
            {
                if (control == config.Control)
                {
                    return baseLeft + config.HorizontalOffset;
                }
            }
            return baseLeft;
        }

        public void UpdateCodecsBlocksPosition()
        {
            double canvasWidth = _mainCanvas.Bounds.Width;
            bool isSmallScreen = canvasWidth < LayoutConfig.ThresholdWidth;
            _viewModel.HeightBlock = isSmallScreen ? 380 : 280;

            double startX = (canvasWidth - _mainBlocks[0].Control.Bounds.Width) / 2 + LayoutConfig.OffsetX;
            double startY = LayoutConfig.BaseY + LayoutConfig.OffsetY;

            if (!isSmallScreen)
            {
                double totalWidth = _mainBlocks.Sum(b => b.Control.Bounds.Width) +
                                   (_mainBlocks.Count - 1) * LayoutConfig.HorizontalSpacing;
                startX = (canvasWidth - totalWidth) / 2 + LayoutConfig.OffsetX;
                double currentX = startX;

                foreach (var config in _mainBlocks)
                {
                    //Console.WriteLine($"Control: {config.Control.Name}, Left={currentX}, Top={startY}");
                    Canvas.SetLeft(config.Control, currentX);
                    Canvas.SetTop(config.Control, startY);
                    currentX += config.Control.Bounds.Width + LayoutConfig.HorizontalSpacing;
                    config.Control.InvalidateMeasure();
                    config.Control.InvalidateArrange();
                    config.Control.InvalidateVisual();
                }
            }
            else
            {
                double currentY = startY;
                foreach (var config in _mainBlocks)
                {
                    //Console.WriteLine($"Control: {config.Control.Name}, Left={startX}, Top={currentY}");
                    Canvas.SetLeft(config.Control, startX);
                    Canvas.SetTop(config.Control, currentY);
                    currentY += config.Control.Bounds.Height + LayoutConfig.VerticalSpacing;
                    config.Control.InvalidateMeasure();
                    config.Control.InvalidateArrange();
                    config.Control.InvalidateVisual();
                }
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
            //Console.WriteLine($"Actualizando GridWidth a: {canvasWidth}, IsSmallScreen: {isSmallScreen}");

            foreach (var config in _secondaryControls)
            {
                double top = _originalTops[config.Control] + (isSmallScreen ? LayoutConfig.MovableOffset : 0);
                Canvas.SetTop(config.Control, top);

                if (config.Control == _secondaryControls[0].Control) // BarraProgreso
                {
                    Canvas.SetLeft(config.Control, 0);
                    //Console.WriteLine($"Control: {config.Control.Name}, Left=0, CanvasWidth={canvasWidth}");
                }
                else
                {
                    double controlWidth = config.Control.DesiredSize.Width > 0
                        ? config.Control.DesiredSize.Width
                        : config.Control.Bounds.Width;

                    double left = config.UseEstimatedWidth
                        ? (canvasWidth - GetEstimatedTextWidth()) / 2
                        : (canvasWidth - controlWidth) / 2;

                    left = AdjustHorizontalCenter(config.Control, left);
                    //Console.WriteLine($"Control: {config.Control.Name}, Left={left}, Width={controlWidth}, Offset={config.HorizontalOffset}");
                    Canvas.SetLeft(config.Control, left);
                }

                config.Control.InvalidateMeasure();
                config.Control.InvalidateArrange();
                config.Control.InvalidateVisual();
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