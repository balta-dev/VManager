using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using ReactiveUI;
using System.ComponentModel;
using Avalonia.Platform.Storage;

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
            if (!e.DataTransfer.Contains(DataFormat.File))
            {
                e.DragEffects = DragDropEffects.None;
                return;
            }

            var files = e.DataTransfer.TryGetFiles();
            if (files == null || !files.Any())
            {
                e.DragEffects = DragDropEffects.None;
                return;
            }

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

            e.DragEffects = DragDropEffects.Copy;
            e.Handled = true;

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

            var files = e.DataTransfer?.TryGetFiles() ?? Array.Empty<IStorageFile>();
            if (!files.Any())
                return;

            bool allowAudio = GetAllowAudio(control);

            var validPaths = files
                .Where(f =>
                {
                    var ext = System.IO.Path.GetExtension(f.Path.LocalPath).ToLowerInvariant();
                    return VideoExtensions.Contains(ext) || (allowAudio && AudioExtensions.Contains(ext));
                })
                .Select(f => f.Path.LocalPath)
                .ToList();

            if (!validPaths.Any())
            {
                ShowErrorFeedback(control);
                return;
            }

            var dc = FindDataContext(control);
            if (dc == null)
                return;

            // Intentamos obtener la propiedad VideoPaths (List<string>)
            var listProp = dc.GetType().GetProperty("VideoPaths");
            var singleProp = dc.GetType().GetProperty("VideoPath");

            if (listProp != null && listProp.CanWrite)
            {
                if (listProp.GetValue(dc) is List<string> currentList)
                {
                    currentList.AddRange(validPaths);
                }
                else
                {
                    listProp.SetValue(dc, new List<string>(validPaths));
                }

                // Forzar notificación ReactiveUI
                if (dc is ReactiveObject reactiveObj)
                {
                    reactiveObj.RaisePropertyChanged("VideoPaths");
                }
            }

            if (singleProp != null && singleProp.CanWrite && validPaths.Any())
            {
                singleProp.SetValue(dc, validPaths.First());
                if (dc is ReactiveObject reactiveObj)
                {
                    reactiveObj.RaisePropertyChanged("VideoPath");
                }
            }

            ShowSuccessFeedback(control);
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