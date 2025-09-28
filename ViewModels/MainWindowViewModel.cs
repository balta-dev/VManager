using System;
using System.Collections.Generic;
using System.Diagnostics;
using ReactiveUI;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Runtime.InteropServices;
using Avalonia.ReactiveUI;
using Avalonia.Styling;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using VManager.Views;
using Avalonia.Styling;

namespace VManager.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    public bool IsWelcomeVisible => CurrentView == null;
    private Herramienta1ViewModel _herramienta1;
    private Herramienta2ViewModel _herramienta2;
    private Herramienta3ViewModel _herramienta3;
    private Herramienta4ViewModel _herramienta4;
    public List<ViewModelBase> Tools { get; }
    
    private bool isVideoPathSet;
    public override bool IsVideoPathSet //NO LO USO ACÁ. ES POR LA CLASE ABSTRACTA. YA SÉ QUE ES MALA PRÁCTICA PERDÓN.
    {
        get => isVideoPathSet;
        set => this.RaiseAndSetIfChanged(ref isVideoPathSet, value);
    }
    
    private bool _herramienta1Activa;
    public bool Herramienta1Activa
    {
        get => _herramienta1Activa;
        set => this.RaiseAndSetIfChanged(ref _herramienta1Activa, value);
    }

    private bool _herramienta2Activa;
    public bool Herramienta2Activa
    {
        get => _herramienta2Activa;
        set => this.RaiseAndSetIfChanged(ref _herramienta2Activa, value);
    }
    
    private bool _herramienta3Activa;
    public bool Herramienta3Activa
    {
        get => _herramienta3Activa;
        set => this.RaiseAndSetIfChanged(ref _herramienta3Activa, value);
    }
    
    private bool _herramienta4Activa;
    public bool Herramienta4Activa
    {
        get => _herramienta4Activa;
        set => this.RaiseAndSetIfChanged(ref _herramienta4Activa, value);
    }
    
    private ViewModelBase? _currentView;
    public ViewModelBase? CurrentView
    {
        get => _currentView;
        set {
            this.RaiseAndSetIfChanged(ref _currentView, value);
            this.RaisePropertyChanged(nameof(IsWelcomeVisible)); // Notifica cambio de visibilidad
            } 
    }
    
    public ReactiveCommand<Unit, Unit> GoToHerramienta1 { get; }
    public ReactiveCommand<Unit, Unit> GoToHerramienta2 { get; }
    public ReactiveCommand<Unit, Unit> GoToHerramienta3 { get; }
    
    public ReactiveCommand<Unit, Unit> GoToHerramienta4 { get; }
    public ReactiveCommand<Unit, Unit> ToggleThemeCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenGitHubCommand { get; }
    
    private bool _isDarkTheme;
    public bool IsDarkTheme
    {
        get => _isDarkTheme;
        set => this.RaiseAndSetIfChanged(ref _isDarkTheme, value);
    }

    public MainWindowViewModel()
    {
        ToggleThemeCommand = ReactiveCommand.Create(ToggleTheme, outputScheduler: AvaloniaScheduler.Instance);
        OpenGitHubCommand = ReactiveCommand.Create(OpenGitHub, outputScheduler: AvaloniaScheduler.Instance);
        
        _herramienta1 = new Herramienta1ViewModel();
        _herramienta2 = new Herramienta2ViewModel();
        _herramienta3 = new Herramienta3ViewModel();
        _herramienta4 = new Herramienta4ViewModel();
        
        Tools = new List<ViewModelBase>
        {
            _herramienta1,
            _herramienta2,
            _herramienta3,
            _herramienta4
        };
        
        GoToHerramienta1 = ReactiveCommand.Create(
            () =>
            {
                Herramienta1Activa = true;
                Herramienta2Activa = false;
                Herramienta3Activa = false;
                Herramienta4Activa = false;
                CurrentView = _herramienta1;
                return Unit.Default;
            },
            outputScheduler: AvaloniaScheduler.Instance
        );

        GoToHerramienta2 = ReactiveCommand.Create(
            () =>
            {
                Herramienta2Activa = true;
                Herramienta1Activa = false;
                Herramienta3Activa = false;
                Herramienta4Activa = false;
                CurrentView = _herramienta2;
                return Unit.Default;
            },
            outputScheduler: AvaloniaScheduler.Instance
        );
        
        GoToHerramienta3 = ReactiveCommand.Create(
            () =>
            {
                Herramienta3Activa = true;
                Herramienta1Activa = false;
                Herramienta2Activa = false;
                Herramienta4Activa = false;
                CurrentView = _herramienta3;
                return Unit.Default;
            },
            outputScheduler: AvaloniaScheduler.Instance
        );
        
        GoToHerramienta4 = ReactiveCommand.Create(
            () =>
            {
                Herramienta4Activa = true;
                Herramienta1Activa = false;
                Herramienta2Activa = false;
                Herramienta3Activa = false;
                CurrentView = _herramienta4;
                return Unit.Default;
            },
            outputScheduler: AvaloniaScheduler.Instance
        );
        
        _isDarkTheme = Application.Current.ActualThemeVariant == ThemeVariant.Dark;

        // Suscribirse a cambios de tema
        Application.Current.GetObservable(Application.ActualThemeVariantProperty)
            .Subscribe(theme =>
            {
                IsDarkTheme = theme == ThemeVariant.Dark;
            });

        ToggleThemeCommand = ReactiveCommand.Create(ToggleTheme, outputScheduler: AvaloniaScheduler.Instance);
    }
    
    private void ToggleTheme()
    {
        var app = (App)Application.Current!;
        app.RequestedThemeVariant = app.ActualThemeVariant == ThemeVariant.Dark
            ? ThemeVariant.Light
            : ThemeVariant.Dark;
    }
    
    private void OpenGitHub()
    {
        string url = "https://github.com/balta-dev";
        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", url);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", url);
            }
        }
        catch (Exception ex)
        {
            // Optionally handle or log the error (e.g., show a message dialog)
            System.Console.WriteLine($"Failed to open GitHub: {ex.Message}");
        }
    }
    
}

