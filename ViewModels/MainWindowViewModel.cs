using ReactiveUI;
using System.Reactive;
using System.Reactive.Concurrency;
using Avalonia.ReactiveUI;
using Avalonia.Styling;
using Avalonia;

namespace VManager.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
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
    public ReactiveCommand<Unit, Unit> ToggleThemeCommand { get; }

    public MainWindowViewModel()
    {
        ToggleThemeCommand = ReactiveCommand.Create(ToggleTheme, outputScheduler: AvaloniaScheduler.Instance);
        
        GoToHerramienta1 = ReactiveCommand.Create(
            () =>
            {
                CurrentView = new Herramienta1ViewModel();
                return Unit.Default; 
            },
            outputScheduler: AvaloniaScheduler.Instance
        );

        GoToHerramienta2 = ReactiveCommand.Create(
            () =>
            {
                CurrentView = new Herramienta2ViewModel();
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
}

