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
    
    public string OutputPath { get; set; } = "";
    public int Progress { get; set; }
    public string Status { get; set; } = "";
    
    private bool _isFileReadyVisible;
    public bool IsFileReadyVisible
    {
        get => _isFileReadyVisible;
        set => this.RaiseAndSetIfChanged(ref _isFileReadyVisible, value);
    }
    
    private string _lastCompressedFilePath;
    
    public ReactiveCommand<Unit, Unit> BrowseCommand { get; }
    public ReactiveCommand<Unit, Unit> ShowFileInFolderCommand { get; }
    public ViewModelBase()
    {
        BrowseCommand = ReactiveCommand.CreateFromTask(BrowseVideo, outputScheduler: AvaloniaScheduler.Instance);
        ShowFileInFolderCommand = ReactiveCommand.Create(ShowFileInFolder, outputScheduler: AvaloniaScheduler.Instance);
    }
    private async Task BrowseVideo()
    {
        //HideFileReadyButton();
        
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
    public void SetLastCompressedFile(string path)
    {
        _lastCompressedFilePath = path;
        IsFileReadyVisible = true;
    }

    public void HideFileReadyButton()
    {
        IsFileReadyVisible = false;
        this.RaisePropertyChanged(nameof(IsFileReadyVisible));
    }

    private void ShowFileInFolder()
    {
        if (string.IsNullOrEmpty(_lastCompressedFilePath))
            return;

        try
        {
            // Abrir el explorador en la carpeta y seleccionar el archivo
            var folder = System.IO.Path.GetDirectoryName(_lastCompressedFilePath)!;
            var file = System.IO.Path.GetFileName(_lastCompressedFilePath);

            if (OperatingSystem.IsWindows())
            {
                System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{_lastCompressedFilePath}\"");
            }
            else if (OperatingSystem.IsLinux())
            {
                // Linux: Nautilus, Dolphin, Thunar, etc.
                System.Diagnostics.Process.Start("xdg-open", folder);
            }
            else if (OperatingSystem.IsMacOS())
            {
                System.Diagnostics.Process.Start("open", folder);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al abrir carpeta: {ex.Message}");
        }
    }
    
}
