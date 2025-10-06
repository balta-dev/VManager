﻿using CommunityToolkit.Mvvm.ComponentModel;
using Avalonia.Controls;
using ReactiveUI;
using Avalonia.ReactiveUI;
using FFMpegCore;
using FFMpegCore.Enums;
using System;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using VManager.Services;
using VManager.Views;

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
    private bool _isClicked;
    private bool _isOperationRunning;
    private bool _isDialogVisible;
    public bool IsDialogVisible
    {
        get => _isDialogVisible;
        set => this.RaiseAndSetIfChanged(ref _isDialogVisible, value);
    }
    public bool IsOperationRunning
    {
        get => _isOperationRunning;
        set => this.RaiseAndSetIfChanged(ref _isOperationRunning, value);
    }
    
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
    public ReactiveCommand<Unit, Unit> ShowCancelDialogCommand { get; }
    
    public LocalizationService L => LocalizationService.Instance;
    
    protected CancellationTokenSource _cts;
    
    public class UserProfileImageService
    {
        public static Bitmap? GetUserProfileImage()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    return GetWindowsUserImage();
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    return GetLinuxUserImage();
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    return GetMacOSUserImage();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error obteniendo imagen de usuario: {ex.Message}");
            }

            return GetDefaultUserImage();
        }

        private static Bitmap? GetWindowsUserImage()
        {
            string userName = Environment.UserName;
            
            // Opción 1: AccountPictures del usuario actual
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string accountPictures = Path.Combine(appData, "Microsoft", "Windows", "AccountPictures");
            
            if (Directory.Exists(accountPictures))
            {
                // Buscar archivos de imagen (jpg, png, bmp)
                var imageFiles = Directory.GetFiles(accountPictures, "*.*")
                    .Where(f => f.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                               f.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                               f.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                    .ToArray();
                
                if (imageFiles.Length > 0)
                {
                    return new Bitmap(imageFiles[0]);
                }
            }

            // Opción 2: Carpeta de datos del usuario en ProgramData
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            string userTilePath = Path.Combine(programData, "Microsoft", "User Account Pictures");
            
            if (Directory.Exists(userTilePath))
            {
                var files = Directory.GetFiles(userTilePath, "*.jpg")
                    .Concat(Directory.GetFiles(userTilePath, "*.png"))
                    .Concat(Directory.GetFiles(userTilePath, "*.bmp"));
                
                foreach (var file in files)
                {
                    if (File.Exists(file))
                    {
                        return new Bitmap(file);
                    }
                }
            }

            return null;
        }

        private static Bitmap? GetLinuxUserImage()
        {
            string userName = Environment.UserName;
            
            // Intentar obtener desde AccountsService
            string accountsServicePath = $"/var/lib/AccountsService/icons/{userName}";
            if (File.Exists(accountsServicePath))
            {
                return new Bitmap(accountsServicePath);
            }

            // Intentar ubicación en home
            string homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string[] possiblePaths = {
                Path.Combine(homePath, ".face"),
                Path.Combine(homePath, ".face.icon"),
                Path.Combine(homePath, ".local", "share", "pixmaps", "faces", userName)
            };

            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    return new Bitmap(path);
                }
            }

            return null;
        }

        private static Bitmap? GetMacOSUserImage()
        {
            string userName = Environment.UserName;
            string homePath = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            
            string profileImagePath = Path.Combine(
                homePath, "Library", "Application Support", 
                "Dock", "desktoppicture.db"
            );

            // En macOS, la imagen suele estar en la carpeta del usuario
            string possiblePath = Path.Combine(
                "/Library/User Pictures",
                $"{userName}.png"
            );

            if (File.Exists(possiblePath))
            {
                return new Bitmap(possiblePath);
            }

            return null;
        }

        public static Bitmap GetDefaultUserImage()
        {
            // Si no se encuentra la imagen, puedes retornar null
            // y manejar el caso en tu UI con un ícono por defecto
            return null;
        }
    }
    public Bitmap UserImage { get; }
    protected ViewModelBase()
    {
        BrowseCommand = ReactiveCommand.CreateFromTask(BrowseVideo, outputScheduler: AvaloniaScheduler.Instance);
        ShowFileInFolderCommand = ReactiveCommand.Create(ShowFileInFolder, outputScheduler: AvaloniaScheduler.Instance);
        ClearInfoCommand = ReactiveCommand.Create(ClearInfo, outputScheduler: AvaloniaScheduler.Instance);
        ShowCancelDialogCommand = ReactiveCommand.CreateFromTask(
            () => ShowCancelDialog(), // <- Usar la versión sin retorno
            outputScheduler: AvaloniaScheduler.Instance
        );
        
        UserImage = UserProfileImageService.GetUserProfileImage() 
                    ?? UserProfileImageService.GetDefaultUserImage();
        
        LocalizationService.Instance.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == "Item[]")
            {
                // Dispara PropertyChanged en todo el binding que use L
                this.RaisePropertyChanged(nameof(LocalizationService));
            }
        };
        
    }
    
    private void MostrarOverlayEnMainWindow()
    {
        // Asegurarse de que hay una ventana principal
        if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = desktop.MainWindow;

            if (mainWindow != null && mainWindow.DataContext is MainWindowViewModel mainVM)
            {
                mainVM.IsDialogVisible = true; // Esto activa el overlay en MainWindow
                Console.WriteLine("Se activó el overlay");
            }
        }
    }
    public async Task<bool> ShowCancelDialogInMainWindow(bool fromWindowClose = false)
    {
        MostrarOverlayEnMainWindow(); // Activa overlay

        // Mostrar el diálogo
        var mainWindow = (Application.Current.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (mainWindow != null)
        {
            var dialog = new CancelDialog();
            bool? result = await dialog.ShowDialog<bool?>(mainWindow);
    
            if (result == true)
            {
                // Cancelar la operación
                Console.WriteLine("Entrando al request cancel...");
                RequestCancelOperation();
            }

            // Desactivar overlay al cerrar el dialog
            if (mainWindow.DataContext is MainWindowViewModel mainVM)
                mainVM.IsDialogVisible = false;
        
            // Retornar true solo si el usuario eligió cancelar Y viene de window close
            return result == true && fromWindowClose;
        }
    
        return false;
    }
    
    public async Task ShowCancelDialog()
    {
        await ShowCancelDialogInMainWindow(fromWindowClose: false);
    }
    
    public void RequestCancelOperation()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            Console.WriteLine("[DEBUG]: Cancelación solicitada por el usuario.");
        }
        // Método virtual que las clases derivadas pueden sobrescribir
        IsOperationRunning = false;
    }
    public abstract bool IsVideoPathSet { get; set; }
    public void ClearInfo()
    {
        Status = "¡Actualizado!";
        Warning = "";
        VideoPath = "";
        Progress = 0;
        OutputPath = "";
        IsFileReadyVisible = false;
        IsVideoPathSet = false;
        IsClicked = false;
        this.RaisePropertyChanged(nameof(Status));
        this.RaisePropertyChanged(nameof(Warning));
        this.RaisePropertyChanged(nameof(VideoPath));
        this.RaisePropertyChanged(nameof(IsFileReadyVisible));
        this.RaisePropertyChanged(nameof(Progress));
        this.RaisePropertyChanged(nameof(OutputPath));
        this.RaisePropertyChanged(nameof(IsVideoPathSet));
        this.RaisePropertyChanged(nameof(IsClicked));
        
        _ = Task.Delay(2000).ContinueWith(_ =>
        {
            Dispatcher.UIThread.InvokeAsync(() =>
            {
                Status = "";
                this.RaisePropertyChanged(nameof(Status));
            });
        });
        
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
            IsVideoPathSet = true;
            this.RaisePropertyChanged(nameof(IsVideoPathSet));
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
    public bool IsClicked
    {
        get => _isClicked;
        set => this.RaiseAndSetIfChanged(ref _isClicked, value);
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

            IsClicked = true;
            this.RaisePropertyChanged(nameof(IsClicked));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error al abrir carpeta: {ex.Message}");
        }
    }
    
}
