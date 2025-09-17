using System;
using System.Diagnostics;
using ReactiveUI;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Runtime.InteropServices;
using Avalonia.ReactiveUI;
using Avalonia.Styling;
using Avalonia;
using VManager.Views;

namespace VManager.ViewModels;

public class MainWindowViewModel : ReactiveObject
{
    private Herramienta1ViewModel _herramienta1;
    private Herramienta2ViewModel _herramienta2;
    private Herramienta3ViewModel _herramienta3;
    
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
    
    private ViewModelBase? _currentView;
    public ViewModelBase? CurrentView
    {
        get => _currentView;
        set {
            this.RaiseAndSetIfChanged(ref _currentView, value);
            this.RaisePropertyChanged(nameof(IsWelcomeVisible)); // Notifica cambio de visibilidad
            } 
    }
    public bool IsWelcomeVisible => CurrentView == null;
    public ReactiveCommand<Unit, Unit> GoToHerramienta1 { get; }
    public ReactiveCommand<Unit, Unit> GoToHerramienta2 { get; }
    public ReactiveCommand<Unit, Unit> GoToHerramienta3 { get; }
    public ReactiveCommand<Unit, Unit> ToggleThemeCommand { get; }
    public ReactiveCommand<Unit, Unit> OpenGitHubCommand { get; }

    public MainWindowViewModel()
    {
        ToggleThemeCommand = ReactiveCommand.Create(ToggleTheme, outputScheduler: AvaloniaScheduler.Instance);
        OpenGitHubCommand = ReactiveCommand.Create(OpenGitHub, outputScheduler: AvaloniaScheduler.Instance);
        
        _herramienta1 = new Herramienta1ViewModel();
        _herramienta2 = new Herramienta2ViewModel();
        _herramienta3 = new Herramienta3ViewModel();
        
        GoToHerramienta1 = ReactiveCommand.Create(
            () =>
            {
                Herramienta1Activa = true;
                Herramienta2Activa = false;
                Herramienta3Activa = false;
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
                CurrentView = _herramienta3;
                return Unit.Default;
            },
            outputScheduler: AvaloniaScheduler.Instance
        );
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

