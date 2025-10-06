using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.ReactiveUI;
using ReactiveUI;
using VManager.Services;
using VManager.Views;

using ReactiveUI;

namespace VManager.ViewModels
{
    public class ConfigurationViewModel : ViewModelBase
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

        private string _idiomaSeleccionado;
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

        private string _openConfig;
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
        
        private bool _useCustomIcon = true;
        public bool UseCustomIcon
        {
            get => _useCustomIcon;
            set
            {
                Console.WriteLine($"UseCustomIcon cambiando de {_useCustomIcon} a {value}");
                this.RaiseAndSetIfChanged(ref _useCustomIcon, value);
                Console.WriteLine($"UseCustomIcon cambió a {_useCustomIcon}");
            }
        }

        public ConfigurationViewModel()
        {
            IdiomaSeleccionado = "Español"; // idioma por defecto
            this.WhenAnyValue(x => x.EnableSounds)
                .Subscribe(val => SoundManager.Enabled = val);
            this.WhenAnyValue(x => x.EnableNotifications)
                .Subscribe(val => Notifier.Enabled = val);

        }
        
    }

}