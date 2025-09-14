using System;
using Avalonia.Controls;
using Avalonia.Input;
using System.Linq;
using VManager.Behaviors;
using Avalonia.Interactivity;
using VManager.Services;

namespace VManager.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            SoundBehavior.Attach(this);
            SoundManager.Play("dummy.wav");
            
            // implementación a medias, no tengo windows para testear que siquiera funcione. capaz debería probar en x11.
            ContentArea.AddHandler(DragDrop.DragOverEvent, DragOverHandler, Avalonia.Interactivity.RoutingStrategies.Tunnel);
            ContentArea.AddHandler(DragDrop.DropEvent, DropHandler, Avalonia.Interactivity.RoutingStrategies.Bubble);
        }

        private void DragOverHandler(object? sender, DragEventArgs e)
        {
            if (e.Data.Contains(DataFormats.FileNames))
                e.DragEffects = DragDropEffects.Copy;
            else
                e.DragEffects = DragDropEffects.None;

            e.Handled = true;
        }

        private void DropHandler(object? sender, DragEventArgs e)
        {
            if (e.Data.Contains(DataFormats.FileNames))
            {
                var files = e.Data.GetFileNames()?.ToList();
                if (files != null)
                {
                    foreach (var file in files)
                    {
                        Console.WriteLine("Archivo recibido: " + file);
                        // aca se puede enviar los archivos al viewmodel teóricamente
                    }
                }
            }
        }
    }
}