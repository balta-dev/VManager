using ReactiveUI;
using System.Reactive;
using System.Reactive.Concurrency;
using Avalonia.ReactiveUI;

namespace VManager.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private ViewModelBase? _currentView;

    public ViewModelBase? CurrentView
    {
        get => _currentView;
        set => this.RaiseAndSetIfChanged(ref _currentView, value);
    }

    public ReactiveCommand<Unit, Unit> GoToHerramienta1 { get; }
    public ReactiveCommand<Unit, Unit> GoToHerramienta2 { get; }

    public MainWindowViewModel()
    {
        // Comandos de navegación
        GoToHerramienta1 = ReactiveCommand.Create(
            () =>
            {
                CurrentView = new Herramienta1ViewModel();
                return Unit.Default; // ✅ esto fuerza el tipo Unit
            },
            outputScheduler: AvaloniaScheduler.Instance
        );

        GoToHerramienta2 = ReactiveCommand.Create(
            () =>
            {
                CurrentView = new Herramienta2ViewModel();
                return Unit.Default; // ✅ mismo aquí
            },
            outputScheduler: AvaloniaScheduler.Instance
        );
    }
}

