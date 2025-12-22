using System;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace VManager.Behaviors
{
    // Eventos globales para comunicar estado del drag
    public static class X11DragFeedback
    {
        public static event EventHandler<DragFeedbackEventArgs>? DragStateChanged;
        
        public static void NotifyDragEnter(bool isValid)
        {
            Console.WriteLine($"[X11DragFeedback] NotifyDragEnter: isValid={isValid}");
            DragStateChanged?.Invoke(null, new DragFeedbackEventArgs 
            { 
                State = DragState.Enter,
                IsValid = isValid 
            });
            Console.WriteLine($"[X11DragFeedback] Subscribers count: {DragStateChanged?.GetInvocationList().Length ?? 0}");
        }
        
        public static void NotifyDragLeave()
        {
            Console.WriteLine("[X11DragFeedback] NotifyDragLeave");
            DragStateChanged?.Invoke(null, new DragFeedbackEventArgs 
            { 
                State = DragState.Leave 
            });
        }
        
        public static void NotifyDropSuccess()
        {
            Console.WriteLine("[X11DragFeedback] NotifyDropSuccess");
            DragStateChanged?.Invoke(null, new DragFeedbackEventArgs 
            { 
                State = DragState.DropSuccess 
            });
        }
        
        public static void NotifyDropError()
        {
            Console.WriteLine("[X11DragFeedback] NotifyDropError");
            DragStateChanged?.Invoke(null, new DragFeedbackEventArgs 
            { 
                State = DragState.DropError 
            });
        }
    }
    
    public enum DragState
    {
        Enter,
        Leave,
        DropSuccess,
        DropError
    }
    
    public class DragFeedbackEventArgs : EventArgs
    {
        public DragState State { get; set; }
        public bool IsValid { get; set; }
    }
    
    // Clase helper para aplicar el feedback visual
    public class X11DragFeedbackApplier
    {
        private readonly Border _border;
        private IBrush? _originalBackground;
        
        public X11DragFeedbackApplier(Border border)
        {
            _border = border;
            Console.WriteLine($"[X11DragFeedbackApplier] Creado para border: {_border.Name ?? "sin nombre"}");
            X11DragFeedback.DragStateChanged += OnDragStateChanged;
        }
        
        private async void OnDragStateChanged(object? sender, DragFeedbackEventArgs e)
        {
            Console.WriteLine($"[X11DragFeedbackApplier] OnDragStateChanged: State={e.State}, IsValid={e.IsValid}");
            
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                Console.WriteLine($"[X11DragFeedbackApplier] Ejecutando en UI Thread, State={e.State}");
                
                switch (e.State)
                {
                    case DragState.Enter:
                        if (_originalBackground == null)
                            _originalBackground = _border.Background;
                        
                        var newBg = e.IsValid 
                            ? new SolidColorBrush(Colors.LightGreen, 0.3)
                            : new SolidColorBrush(Colors.LightCoral, 0.3);
                        
                        Console.WriteLine($"[X11DragFeedbackApplier] Cambiando background a {(e.IsValid ? "verde" : "rojo")}");
                        _border.Background = newBg;
                        break;
                        
                    case DragState.Leave:
                        RestoreBackground();
                        break;
                        
                    case DragState.DropSuccess:
                        ShowSuccessFeedback();
                        break;
                        
                    case DragState.DropError:
                        ShowErrorFeedback();
                        break;
                }
            });
        }
        
        private void RestoreBackground()
        {
            Console.WriteLine("[X11DragFeedbackApplier] RestoreBackground");
            _border.Background = _originalBackground ?? Brushes.Transparent;
            _originalBackground = null;
        }
        
        private async void ShowSuccessFeedback()
        {
            Console.WriteLine("[X11DragFeedbackApplier] ShowSuccessFeedback");
            _border.Background = new SolidColorBrush(Colors.LightGreen, 0.6);
            await System.Threading.Tasks.Task.Delay(200);
            _border.Background = Brushes.Transparent;
            _originalBackground = null;
        }
        
        private async void ShowErrorFeedback()
        {
            Console.WriteLine("[X11DragFeedbackApplier] ShowErrorFeedback");
            _border.Background = new SolidColorBrush(Colors.LightCoral, 0.6);
            await System.Threading.Tasks.Task.Delay(300);
            _border.Background = Brushes.Transparent;
            _originalBackground = null;
        }
        
        public void Dispose()
        {
            X11DragFeedback.DragStateChanged -= OnDragStateChanged;
        }
    }
}