using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Controls;
using ReactiveUI;
using Avalonia.ReactiveUI;
using FFMpegCore;
using FFMpegCore.Enums;
using System;
using System.IO;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;

namespace VManager.ViewModels;

public class ViewModelBase : ReactiveObject
{
    public string VideoPath { get; set; } = "";
    public int Progress { get; set; }
    public string Status { get; set; } = "";
    
    public ReactiveCommand<Unit, Unit> BrowseCommand { get; }
    public ViewModelBase()
    {
        BrowseCommand = ReactiveCommand.CreateFromTask(BrowseVideo, outputScheduler: AvaloniaScheduler.Instance);
    }
    private async Task BrowseVideo()
    {
        TopLevel? topLevel = null; 
        if (App.Current != null && 
            App.Current.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop && 
            desktop.MainWindow != null) 
        {
            topLevel = TopLevel.GetTopLevel(desktop.MainWindow);
        }
            
        if (topLevel == null)
        {
            Status = App.Current == null || App.Current.ApplicationLifetime is not Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime
                ? "El entorno de la aplicación no es compatible con la ventana principal."
                : "La ventana principal no está inicializada.";
            this.RaisePropertyChanged(nameof(Status));
            return;
        }

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Seleccionar Video",
            FileTypeFilter = new[] { new FilePickerFileType("Videos") { Patterns = new[] { "*.mp4", "*.mkv", "*.mov" } } },
            AllowMultiple = false
        });

        if (files.Count > 0)
        {
            VideoPath = files[0].Path.LocalPath;
            this.RaisePropertyChanged(nameof(VideoPath));
        }
            
    }
    
}
