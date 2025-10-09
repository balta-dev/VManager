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
        // Enable drag & drop
        public static readonly AttachedProperty<bool> EnableFileDropProperty =
            AvaloniaProperty.RegisterAttached<Control, bool>(
                "EnableFileDrop", typeof(DragDropHelper));

        public static bool GetEnableFileDrop(Control control) => control.GetValue(EnableFileDropProperty);
        public static void SetEnableFileDrop(Control control, bool value) => control.SetValue(EnableFileDropProperty, value);

        // ViewModel property where files are written
        public static readonly AttachedProperty<string> DropTargetProperty =
            AvaloniaProperty.RegisterAttached<Control, string>(
                "DropTarget", typeof(DragDropHelper));

        public static string GetDropTarget(Control control) => control.GetValue(DropTargetProperty);
        public static void SetDropTarget(Control control, string value) => control.SetValue(DropTargetProperty, value);

        // Only video and audio
        private static readonly string[] VideoExtensions = 
        {
            ".mp4", ".avi", ".mkv", ".mov", ".wmv", ".flv", ".webm", ".3gp"
        };
        
        private static readonly string[] AudioExtensions = 
        {
            ".mp3", ".wav", ".flac", ".aac", ".ogg", ".wma", ".m4a"
        };
        

        // Simple property to also enable audio
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
                    DragDrop.SetAllowDrop(control, true);
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

        // Save original background
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

            // Verify valid files
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
                    // RED VISUAL FEEDBACK
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

            // Valid
            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;
            
            // Green visual feedback
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
            // Restore original color on drag leave
            if (sender is Border border)
            {
                var mousePos = e.GetPosition(border);
                // Si el cursor sigue dentro del border, no hacemos nada
                if (mousePos.X >= 0 && mousePos.X <= border.Bounds.Width &&
                    mousePos.Y >= 0 && mousePos.Y <= border.Bounds.Height)
                    return;

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

            // Filter only video/audio
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
                // No valid files (error feedback)
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
                
                // force reactive notification
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
                
                // success feedback! :)
                ShowSuccessFeedback(control);
            }

            e.Handled = true;
        }

        private static async void ShowSuccessFeedback(Control control)
        {
            if (control is Border border)
            {
                // Check for clean background
                var originalBackground = border.GetValue(OriginalBackgroundProperty) ?? border.Background;
                
                // Green flash
                border.Background = new SolidColorBrush(Colors.LightGreen, 0.6);
                await System.Threading.Tasks.Task.Delay(200);
                border.Background = originalBackground ?? Brushes.Transparent;
                
                // Clear reference
                border.ClearValue(OriginalBackgroundProperty);
            }
        }

        private static async void ShowErrorFeedback(Control control)
        {
            if (control is Border border)
            {
                // Check for clean background
                var originalBackground = border.GetValue(OriginalBackgroundProperty) ?? border.Background;
                
                // Red flash
                border.Background = new SolidColorBrush(Colors.LightCoral, 0.6);
                await System.Threading.Tasks.Task.Delay(300);
                border.Background = originalBackground ?? Brushes.Transparent;
                
                // Clear reference
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