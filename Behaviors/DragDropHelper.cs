using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using ReactiveUI;
using System.ComponentModel;

namespace VManager.Behaviors
{
    public static class DragDropHelper
    {
        // Habilitar drag & drop
        public static readonly AttachedProperty<bool> EnableFileDropProperty =
            AvaloniaProperty.RegisterAttached<Control, bool>(
                "EnableFileDrop", typeof(DragDropHelper));

        public static bool GetEnableFileDrop(Control control) => control.GetValue(EnableFileDropProperty);
        public static void SetEnableFileDrop(Control control, bool value) => control.SetValue(EnableFileDropProperty, value);

        // Propiedad del ViewModel donde se escriben los archivos
        public static readonly AttachedProperty<string> DropTargetProperty =
            AvaloniaProperty.RegisterAttached<Control, string>(
                "DropTarget", typeof(DragDropHelper));

        public static string GetDropTarget(Control control) => control.GetValue(DropTargetProperty);
        public static void SetDropTarget(Control control, string value) => control.SetValue(DropTargetProperty, value);

        // Solo formatos de video y audio para tu app
        private static readonly string[] VideoExtensions = 
        {
            ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".3gp"
        };
        
        private static readonly string[] AudioExtensions = 
        {
            ".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma", ".m4a"
        };
        

        // Propiedad simple para habilitar audio además de video
        public static readonly AttachedProperty<bool> AllowAudioProperty =
            AvaloniaProperty.RegisterAttached<Control, bool>(
                "AllowAudio", typeof(DragDropHelper));

        public static bool GetAllowAudio(Control control) => control.GetValue(AllowAudioProperty);
        public static void SetAllowAudio(Control control, bool value) => control.SetValue(AllowAudioProperty, value);

        static DragDropHelper()
        {
            EnableFileDropProperty.Changed.AddClassHandler<Control>((control, e) =>
            {
                if (e.NewValue is bool enabled && enabled)
                {
                    // ¡ESTO ES LO QUE FALTABA!
                    DragDrop.SetAllowDrop(control, true);
                    
                    // También necesitás manejar DragOver para indicar que acepta el drop
                    control.AddHandler(DragDrop.DragOverEvent, OnDragOver, handledEventsToo: true);
                    control.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave, handledEventsToo: true);
                    control.AddHandler(DragDrop.DropEvent, OnDropFile, handledEventsToo: true);
                }
                else
                {
                    DragDrop.SetAllowDrop(control, false);
                    control.RemoveHandler(DragDrop.DragOverEvent, OnDragOver);
                    control.RemoveHandler(DragDrop.DragLeaveEvent, OnDragLeave);
                    control.RemoveHandler(DragDrop.DropEvent, OnDropFile);
                }
            });
        }

        // Guardar el background original para restaurarlo
        private static readonly AttachedProperty<IBrush?> OriginalBackgroundProperty =
            AvaloniaProperty.RegisterAttached<Control, IBrush?>("OriginalBackground", typeof(DragDropHelper));

        private static void OnDragOver(object? sender, DragEventArgs e)
        {
            if (!e.Data.Contains(DataFormats.Files))
            {
                e.DragEffects = DragDropEffects.None;
                return;
            }

            var files = e.Data.GetFiles();
            if (files == null || !files.Any())
            {
                e.DragEffects = DragDropEffects.None;
                return;
            }

            // Verificar si hay archivos válidos (video/audio)
            if (sender is Control control)
            {
                bool allowAudio = GetAllowAudio(control);
                
                bool hasValidFile = files.Any(file =>
                {
                    var extension = System.IO.Path.GetExtension(file.Path.LocalPath).ToLowerInvariant();
                    return VideoExtensions.Contains(extension) || 
                           (allowAudio && AudioExtensions.Contains(extension));
                });

                if (!hasValidFile)
                {
                    // Archivo no válido - feedback visual rojo
                    e.DragEffects = DragDropEffects.None;
                    
                    if (sender is Border border)
                    {
                        if (border.GetValue(OriginalBackgroundProperty) == null)
                        {
                            border.SetValue(OriginalBackgroundProperty, border.Background);
                        }
                        border.Background = new SolidColorBrush(Colors.LightCoral, 0.3);
                    }
                    return;
                }
            }

            // Archivo válido
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
            
            // Color verde para archivos válidos
            if (sender is Border validBorder)
            {
                if (validBorder.GetValue(OriginalBackgroundProperty) == null)
                {
                    validBorder.SetValue(OriginalBackgroundProperty, validBorder.Background);
                }
                
                validBorder.Background = new SolidColorBrush(Colors.LightGreen, 0.3);
            }
        }

        private static void OnDragLeave(object? sender, DragEventArgs e)
        {
            // Restaurar el color original cuando se va el drag
            if (sender is Border border)
            {
                var originalBackground = border.GetValue(OriginalBackgroundProperty);
                border.Background = originalBackground ?? Brushes.Transparent;
                border.ClearValue(OriginalBackgroundProperty);
            }
        }

        private static void OnDropFile(object? sender, DragEventArgs e)
        {
            if (sender is not Control control)
                return;

            var propName = GetDropTarget(control);
            if (string.IsNullOrEmpty(propName))
                return;

            var files = e.Data.GetFiles();
            if (files == null || !files.Any())
                return;

            // Filtrar solo archivos de video/audio válidos
            bool allowAudio = GetAllowAudio(control);
            
            var validPaths = files.Where(file =>
            {
                var extension = System.IO.Path.GetExtension(file.Path.LocalPath).ToLowerInvariant();
                return VideoExtensions.Contains(extension) || 
                       (allowAudio && AudioExtensions.Contains(extension));
            })
            .Select(f => f.Path.LocalPath);

            if (!validPaths.Any())
            {
                // No hay archivos válidos - mostrar feedback de error
                ShowErrorFeedback(control);
                return;
            }

            var joined = string.Join(" ", validPaths);

            var dc = FindDataContext(control);
            if (dc == null)
                return;

            var prop = dc.GetType().GetProperty(propName);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(dc, joined);
                
                // FORZAR NOTIFICACIÓN PARA REACTIVEUI
                if (dc is ReactiveObject reactiveObj)
                {
                    reactiveObj.RaisePropertyChanged(propName);
                }
                else if (dc is INotifyPropertyChanged notifyObj)
                {
                    var propertyChangedField = dc.GetType()
                        .GetField("PropertyChanged", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                        
                    if (propertyChangedField?.GetValue(dc) is PropertyChangedEventHandler handler)
                    {
                        handler.Invoke(dc, new PropertyChangedEventArgs(propName));
                    }
                }
                
                // FEEDBACK VISUAL DE ÉXITO
                ShowSuccessFeedback(control);
            }

            e.Handled = true;
        }

        private static async void ShowSuccessFeedback(Control control)
        {
            if (control is Border border)
            {
                // Asegurarse de que el background esté limpio primero
                var originalBackground = border.GetValue(OriginalBackgroundProperty) ?? border.Background;
                
                // Flash verde para éxito
                border.Background = new SolidColorBrush(Colors.LightGreen, 0.6);
                await System.Threading.Tasks.Task.Delay(200);
                border.Background = originalBackground ?? Brushes.Transparent;
                
                // Limpiar la referencia
                border.ClearValue(OriginalBackgroundProperty);
            }
        }

        private static async void ShowErrorFeedback(Control control)
        {
            if (control is Border border)
            {
                // Asegurarse de que el background esté limpio primero
                var originalBackground = border.GetValue(OriginalBackgroundProperty) ?? border.Background;
                
                // Flash rojo para error
                border.Background = new SolidColorBrush(Colors.LightCoral, 0.6);
                await System.Threading.Tasks.Task.Delay(300);
                border.Background = originalBackground ?? Brushes.Transparent;
                
                // Limpiar la referencia
                border.ClearValue(OriginalBackgroundProperty);
            }
        }

        private static object? FindDataContext(Control control)
        {
            var current = control;
            while (current != null)
            {
                if (current.DataContext != null)
                    return current.DataContext;
                current = current.Parent as Control;
            }
            return null;
        }
    }
}