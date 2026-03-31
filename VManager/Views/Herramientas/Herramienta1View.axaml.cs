using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Input;
using VManager.Behaviours.X11DragDrop;
using VManager.ViewModels.Herramientas;
using RSControls = RangeSlider.Avalonia.Controls;

namespace VManager.Views.Herramientas
{
    public partial class Herramienta1View : SoundEnabledUserControl
    {
        private X11DragFeedbackApplier? _feedbackApplier;

        public Herramienta1View()
        {
            InitializeComponent();
            var border = this.FindControl<Border>("DropZoneBorder");
            if (border != null && OperatingSystem.IsLinux())
                _feedbackApplier = new X11DragFeedbackApplier(border);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            var slider = this.FindControl<RSControls.RangeSlider>("TheSlider");
            if (slider == null) return;

            // Actualizar ancho del track cuando cambia el tamaño
            slider.GetObservable(Layoutable.BoundsProperty).Subscribe(bounds =>
            {
                // Guard: ignorar bounds inválidos durante layout passes intermedios
                if (bounds.Width <= 0 || double.IsNaN(bounds.Width)) return;
                
                if (DataContext is Herramienta1ViewModel vm)
                    vm.SliderTrackWidth = bounds.Width;
            });

            // Iniciar/detener el drag loop
            slider.AddHandler(PointerPressedEvent, (_, _) =>
            {
                if (DataContext is Herramienta1ViewModel vm)
                    vm.StartDragging();
            }, handledEventsToo: true);

            slider.AddHandler(PointerReleasedEvent, (_, _) =>
            {
                if (DataContext is Herramienta1ViewModel vm)
                    vm.StopDragging();
            }, handledEventsToo: true);
        }
    }
}