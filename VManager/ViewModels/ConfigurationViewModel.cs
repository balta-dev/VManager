using System;
using System.Collections.ObjectModel;
using System.Reactive;
using System.Reactive.Linq;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.ReactiveUI;
using ReactiveUI;
using VManager.Services;
using VManager.Views;

namespace VManager.ViewModels
{
    public class ConfigurationViewModel : CodecViewModelBase
    {
        private bool isVideoPathSet; //por ser abstract
        public override bool IsVideoPathSet
        {
            get => isVideoPathSet;
            set => this.RaiseAndSetIfChanged(ref isVideoPathSet, value);
        }
        
        public ObservableCollection<string> Idiomas { get; } = new()
        {
            "Español",
            "English"
        };
        
        private bool _enableNotifications = true;
        public bool EnableNotifications
        {
            get => _enableNotifications;
            set => this.RaiseAndSetIfChanged(ref _enableNotifications, value);
        }
        
        private bool _hideRemainingTime = true;
        public bool HideRemainingTime
        {
            get => _hideRemainingTime;
            set => this.RaiseAndSetIfChanged(ref _hideRemainingTime, value);
        }

        private string _idiomaSeleccionado = "";
        public string IdiomaSeleccionado
        {
            get => _idiomaSeleccionado;
            set
            {
                this.RaiseAndSetIfChanged(ref _idiomaSeleccionado, value);

                // Cambiar el idioma de LocalizationService según la selección
                switch (value)
                {
                    case "Español":
                        LocalizationService.Instance.CurrentLanguage = "es";
                        OpenConfig = $"¡Bienvenid@, {char.ToUpper(Environment.UserName[0]) + Environment.UserName.Substring(1)}! ¿Qué necesitas cambiar? :)";
                        break;
                    case "English":
                        LocalizationService.Instance.CurrentLanguage = "en";
                        OpenConfig = $"Welcome, {char.ToUpper(Environment.UserName[0]) + Environment.UserName.Substring(1)}! What do you want to change? :)";
                        break;
                }

                // Notificar que los bindings podrían necesitar refrescar
                this.RaisePropertyChanged(nameof(OpenConfig));
                
                var mainVM = App.Current?.ApplicationLifetime switch
                {
                    Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop =>
                        desktop.MainWindow?.DataContext as MainWindowViewModel,
                    _ => null
                };

                if (mainVM != null && mainVM.CurrentView == mainVM._configuration)
                {
                    // Reasigna la vista para forzar renderizado
                    mainVM.CurrentView = null;
                    mainVM.CurrentView = mainVM._configuration;
                }
                
            }
        }

        private string _openConfig = "";
        public string OpenConfig
        {
            get => _openConfig;
            set => this.RaiseAndSetIfChanged(ref _openConfig, value);
        }
        
        private bool _enableSounds = SoundManager.Enabled;
        public bool EnableSounds
        {
            get => _enableSounds;
            set => this.RaiseAndSetIfChanged(ref _enableSounds, value);
        }
        
        private bool _useCustomIcon = false;
        public bool UseCustomIcon
        {
            get => _useCustomIcon;
            set => this.RaiseAndSetIfChanged(ref _useCustomIcon, value);
            
        }
        
        private Color? _selectedColor;
        public Color? SelectedColor
        {
            get => _selectedColor;
            set => this.RaiseAndSetIfChanged(ref _selectedColor, value);
        }
        public ReactiveCommand<Unit, Unit> RefreshCodecsCommand { get; }
        public ReactiveCommand<Unit, Unit> ResetAccentColorCommand { get; }
        
        private readonly ConfigurationService.AppConfig _config;
        
        public ConfigurationViewModel()
        {
            RefreshCodecsCommand = ReactiveCommand.CreateFromTask(ReloadCodecsAsync, outputScheduler: AvaloniaScheduler.Instance);
            ResetAccentColorCommand = ReactiveCommand.Create<Unit>(
                () =>
                {
                    SelectedColor = null;
                    SelectedColor = null; // necesito ponerlo por segunda vez, por algun motivo no funciona distinto
                    return Unit.Default; // <- importante para que sea Unit
                },
                outputScheduler: AvaloniaScheduler.Instance
            );
            
            _config = ConfigurationService.Load();

            // Inicializar propiedades
            IdiomaSeleccionado = _config.Language;
            EnableSounds = _config.EnableSounds;
            EnableNotifications = _config.EnableNotifications;
            UseCustomIcon = _config.UseCustomIcon;
            HideRemainingTime = _config.HideRemainingTime;
            SelectedColor = _config.SelectedColor;

            // Guardar cambios automáticamente
            // Suscribirse a cambios de EnableSounds y EnableNotifications
            this.WhenAnyValue(x => x.EnableSounds)
                .Subscribe(val =>
                {
                    SoundManager.Enabled = val;     // Actúa en la app
                    SaveConfig();                   // Guarda en JSON
                });

            this.WhenAnyValue(x => x.EnableNotifications)
                .Subscribe(val =>
                {
                    Notifier.Enabled = val;         // Actúa en la app
                    SaveConfig();                   // Guarda en JSON
                });
            
            // Otros campos que quieras guardar automáticamente
            this.WhenAnyValue(x => x.IdiomaSeleccionado, x => x.UseCustomIcon, x => x.HideRemainingTime, x => x.SelectedColor)
                .Subscribe(_ => SaveConfig());
            
        }

        private void SaveConfig()
        {
            _config.Language = IdiomaSeleccionado;
            _config.EnableSounds = EnableSounds;
            _config.EnableNotifications = EnableNotifications;
            _config.UseCustomIcon = UseCustomIcon;
            _config.HideRemainingTime = HideRemainingTime;
            _config.SelectedColor = SelectedColor;

            ConfigurationService.Save(_config);
        }
        
    }

}