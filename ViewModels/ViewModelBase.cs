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

public abstract class ViewModelBase : ReactiveObject
{
    private string _videoPath = "";
    private string _outputPath = "";
    private string _lastCompressedFilePath;
    private int _progress;
    private string _status = "";
    private string _warning = "";
    private bool _isFileReadyVisible;

    public string VideoPath
    {
        get => _videoPath;
        set => this.RaiseAndSetIfChanged(ref _videoPath, value);
    }

    public string OutputPath
    {
        get => _outputPath;
        set => this.RaiseAndSetIfChanged(ref _outputPath, value);
    }

    public int Progress
    {
        get => _progress;
        set => this.RaiseAndSetIfChanged(ref _progress, value);
    }

    public string Status
    {
        get => _status;
        set => this.RaiseAndSetIfChanged(ref _status, value);
    }

    public string Warning
    {
        get => _warning;
        set => this.RaiseAndSetIfChanged(ref _warning, value);
    }

    public bool IsFileReadyVisible
    {
        get => _isFileReadyVisible;
        set => this.RaiseAndSetIfChanged(ref _isFileReadyVisible, value);
    }

    public ReactiveCommand<Unit, Unit> BrowseCommand { get; }
    public ReactiveCommand<Unit, Unit> ShowFileInFolderCommand { get; }
    public ReactiveCommand<Unit, Unit> ClearInfoCommand { get; }

    protected ViewModelBase()
    {
        BrowseCommand = ReactiveCommand.CreateFromTask(BrowseVideo, outputScheduler: AvaloniaScheduler.Instance);
        ShowFileInFolderCommand = ReactiveCommand.Create(ShowFileInFolder, outputScheduler: AvaloniaScheduler.Instance);
        ClearInfoCommand = ReactiveCommand.Create(ClearInfo, outputScheduler: AvaloniaScheduler.Instance);
    }

    private void ClearInfo()
    {
        Status = "";
        Warning = "";
        VideoPath = "";
        Progress = 0;
        OutputPath = "";
        IsFileReadyVisible = false;
        this.RaisePropertyChanged(nameof(Status));
        this.RaisePropertyChanged(nameof(Warning));
        this.RaisePropertyChanged(nameof(VideoPath));
        this.RaisePropertyChanged(nameof(IsFileReadyVisible));
        this.RaisePropertyChanged(nameof(Progress));
        this.RaisePropertyChanged(nameof(OutputPath));
        
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
