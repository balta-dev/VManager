using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.ReactiveUI;
using ReactiveUI;
using VManager.Services;
using VManager.Services.Core;
using System.Diagnostics;
using VManager.Services.Models;
using Avalonia.Styling;

namespace VManager.ViewModels
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
    public class ConfigurationViewModel : CodecViewModelBase
    {
        
        public ObservableCollection<string> Idiomas { get; } = new()
        {
            "English",
            "Español",
            "Français",
            "Deutsch",
            "Italiano",
            "Polski",
            "Português",
            "Русский",
            "Українська",
            "हिंदी",
            "日本語",
            "한국어",
            "中文",
            "عربي"
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

                switch (value)
                {
                    case "English": LocalizationService.Instance.CurrentLanguage = "en"; break;
                    case "Español": LocalizationService.Instance.CurrentLanguage = "es"; break;
                    case "Français": LocalizationService.Instance.CurrentLanguage = "fr"; break;
                    case "Deutsch":   LocalizationService.Instance.CurrentLanguage = "de"; break;
                    case "Italiano":  LocalizationService.Instance.CurrentLanguage = "it"; break;
                    case "Polski":    LocalizationService.Instance.CurrentLanguage = "pl"; break;
                    case "Português": LocalizationService.Instance.CurrentLanguage = "pt"; break;
                    case "Русский": LocalizationService.Instance.CurrentLanguage = "ru"; break;
                    case "Українська": LocalizationService.Instance.CurrentLanguage = "uk"; break;
                    case "हिंदी":     LocalizationService.Instance.CurrentLanguage = "hi"; break;
                    case "日本語": LocalizationService.Instance.CurrentLanguage = "jp"; break;
                    case "한국어":    LocalizationService.Instance.CurrentLanguage = "ko"; break;
                    case "中文": LocalizationService.Instance.CurrentLanguage = "zh"; break;
                    case "عربي": LocalizationService.Instance.CurrentLanguage = "ar"; break;
                }
                
                OpenConfig = string.Format(L["Configuration.Fields.Welcome"], (Environment.UserName[0]) + Environment.UserName.Substring(1));

                this.RaisePropertyChanged(nameof(OpenConfig));
                
                var mainVM = App.Current?.ApplicationLifetime switch
                {
                    Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop =>
                        desktop.MainWindow?.DataContext as MainWindowViewModel,
                    _ => null
                };

                if (mainVM != null && mainVM.CurrentView == mainVM._configuration)
                {
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
        
        private Bitmap? _profileImageBitmap;
        public Bitmap? ProfileImageBitmap
        {
            get => _profileImageBitmap;
            set => this.RaiseAndSetIfChanged(ref _profileImageBitmap, value);
        }
        
        private string? _profileImagePath;
        public string? ProfileImagePath
        {
            get => _profileImagePath;
            set
            {
                this.RaiseAndSetIfChanged(ref _profileImagePath, value);
                LoadProfileImageBitmap();
            }
        }
        
        private bool _hasProfileImage;
        public bool HasProfileImage
        {
            get => _hasProfileImage;
            set => this.RaiseAndSetIfChanged(ref _hasProfileImage, value);
        }
        
        private string? _preferredDownloadFolder;
        public string? PreferredDownloadFolder
        {
            get => _preferredDownloadFolder;
            set => this.RaiseAndSetIfChanged(ref _preferredDownloadFolder, value);
        }
        
        // --- Propiedades relacionadas con cookies ---
        private bool _useCookiesFile;
        public bool UseCookiesFile
        {
            get => _useCookiesFile;
            set => this.RaiseAndSetIfChanged(ref _useCookiesFile, value);
        }

        private string? _cookiesFilePath;
        public string? CookiesFilePath
        {
            get => _cookiesFilePath;
            set => this.RaiseAndSetIfChanged(ref _cookiesFilePath, value);
        }

        private DateTime? _cookiesLastUpdated;
        public DateTime? CookiesLastUpdated
        {
            get => _cookiesLastUpdated;
            set
            {
                this.RaiseAndSetIfChanged(ref _cookiesLastUpdated, value);
                this.RaisePropertyChanged(nameof(CookiesLastUpdated));
            }
        }
        
        private bool _log;
        public bool Log
        {
            get => _log;
            set => this.RaiseAndSetIfChanged(ref _log, value);
        }
        
        private bool? _useDarkTheme;
        public bool? UseDarkTheme
        {
            get => _useDarkTheme;
            set => this.RaiseAndSetIfChanged(ref _useDarkTheme, value);
        }
        
        public ReactiveCommand<Unit, Unit> RefreshCodecsCommand { get; }
        public ReactiveCommand<Unit, Unit> ResetAccentColorCommand { get; }
        public ReactiveCommand<Unit, Unit> BrowseProfileImageCommand { get; }
        public ReactiveCommand<Unit, Unit> RemoveProfileImageCommand { get; }
        public ReactiveCommand<Unit, Unit> BrowseDownloadFolderCommand { get; }
        public ReactiveCommand<Unit, Unit> BrowseCookiesFileCommand { get; }
        public ReactiveCommand<Unit, Unit> RemoveCookiesFileCommand { get; }
        
        public ReactiveCommand<Unit, Unit> OpenLogsFolderCommand { get; }
        
        private readonly AppConfig _config;
        
        public void OpenLogsFolder()
        {
            var logsFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "VManager", "logs");
    
            Directory.CreateDirectory(logsFolder); // por si no existe
            Process.Start(new ProcessStartInfo(logsFolder) { UseShellExecute = true });
        }
        
        private async Task BrowseDownloadFolderAsync()
        {
            TopLevel? topLevel = null;

            if (App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow != null)
            {
                topLevel = TopLevel.GetTopLevel(desktop.MainWindow);
            }

            if (topLevel == null)
            {
                Status = L["Configuration.Fields.MainWindowFail"];
                return;
            }

            var folder = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = L["Configuration.Fields.BrowseDownloadFolder"],
                AllowMultiple = false
            });

            if (folder.Count > 0)
            {
                PreferredDownloadFolder = folder[0].Path.LocalPath;
                Status = L["Configuration.Fields.DownloadFolderSet"];
            }
        }
        
        public ConfigurationViewModel()
        {
            RefreshCodecsCommand = ReactiveCommand.CreateFromTask(ReloadCodecsAsync, outputScheduler: AvaloniaScheduler.Instance);
            OpenLogsFolderCommand = ReactiveCommand.Create(OpenLogsFolder, outputScheduler: AvaloniaScheduler.Instance);
            
            ResetAccentColorCommand = ReactiveCommand.Create<Unit>(
                () =>
                {
                    SelectedColor = null;
                    SelectedColor = null;
                    return Unit.Default;
                },
                outputScheduler: AvaloniaScheduler.Instance
            );
            
            BrowseProfileImageCommand = ReactiveCommand.CreateFromTask(BrowseProfileImage, outputScheduler: AvaloniaScheduler.Instance);
            RemoveProfileImageCommand = ReactiveCommand.CreateFromTask(RemoveProfileImage, outputScheduler: AvaloniaScheduler.Instance);
            BrowseDownloadFolderCommand = ReactiveCommand.CreateFromTask(BrowseDownloadFolderAsync, outputScheduler: AvaloniaScheduler.Instance);
            BrowseCookiesFileCommand = ReactiveCommand.CreateFromTask(BrowseCookiesFileAsync, outputScheduler: AvaloniaScheduler.Instance);
            RemoveCookiesFileCommand = ReactiveCommand.CreateFromTask(RemoveCookiesFileAsync, outputScheduler: AvaloniaScheduler.Instance);
            
            _config = ConfigurationService.Current;

            // Inicializar propiedades
            IdiomaSeleccionado = _config.Language;
            EnableSounds = _config.EnableSounds;
            EnableNotifications = _config.EnableNotifications;
            UseCustomIcon = _config.UseCustomIcon;
            HideRemainingTime = _config.HideRemainingTime;
            SelectedColor = _config.SelectedColor;
            ProfileImagePath = _config.ProfileImagePath ?? ProfileImageService.GetCurrentProfileImagePath();
            HasProfileImage = !string.IsNullOrEmpty(ProfileImagePath);
            PreferredDownloadFolder = _config.PreferredDownloadFolder;
            UseCookiesFile = _config.UseCookiesFile;
            CookiesFilePath = _config.CookiesFilePath;
            CookiesLastUpdated = _config.CookiesLastUpdated;
            Log = _config.Log;
            UseDarkTheme = _config.UseDarkTheme;

            // Guardar cambios automáticamente
            this.WhenAnyValue(x => x.EnableSounds)
                .Subscribe(val =>
                {
                    SoundManager.Enabled = val;
                    SaveConfig();
                });

            this.WhenAnyValue(x => x.EnableNotifications)
                .Subscribe(val =>
                {
                    NotificationService.Enabled = val;
                    SaveConfig();
                });
            
            this.WhenAnyValue(x => x.IdiomaSeleccionado, x => x.UseCustomIcon, x => x.HideRemainingTime, x => x.SelectedColor, x => x.ProfileImagePath, x => x.PreferredDownloadFolder, x => x.Log)
                .Subscribe(_ => SaveConfig());
            
        }
        
        private async Task BrowseCookiesFileAsync()
        {
            TopLevel? topLevel = null;

            if (App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow != null)
            {
                topLevel = TopLevel.GetTopLevel(desktop.MainWindow);
            }

            if (topLevel == null)
            {
                Status = L["Configuration.Fields.MainWindowFail"];
                return;
            }

            var filters = new List<FilePickerFileType>
            {
                new FilePickerFileType(L["Configuration.Fields.CookiesFile"]) { Patterns = new[] { "*.txt", "*.cookies" } }
            };

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = L["Configuration.Fields.BrowseCookies"],
                FileTypeFilter = filters,
                AllowMultiple = false
            });

            if (files.Count > 0)
            {
                var local = files[0].Path.LocalPath;

                if (!File.Exists(local) || new FileInfo(local).Length == 0)
                {
                    Status = L["Configuration.Fields.InvalidCookiesFile"];
                    return;
                }

                CookiesFilePath = local;
                CookiesLastUpdated = DateTime.Now;
                UseCookiesFile = true;
                Status = L["Configuration.Fields.CookiesSet"];
                SaveConfig();
            }
        }

        private Task RemoveCookiesFileAsync()
        {
            CookiesFilePath = null;
            CookiesLastUpdated = null;
            UseCookiesFile = false;

            Status = L["Configuration.Fields.CookiesRemoved"];
            SaveConfig();
            return Task.CompletedTask;
        }

        private async Task BrowseProfileImage()
        {
            TopLevel? topLevel = null;

            if (App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow != null)
            {
                topLevel = TopLevel.GetTopLevel(desktop.MainWindow);
            }

            if (topLevel == null)
            {
                Status = L["Configuration.Fields.MainWindowFail"];
                return;
            }

            var imagePatterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.gif", "*.bmp", "*.tiff", "*.webp" };

            var filters = new List<FilePickerFileType>
            {
                new FilePickerFileType(L["Configuration.Fields.Images"]) { Patterns = imagePatterns }
            };

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = L["Configuration.Fields.BrowseProfileImage"],
                FileTypeFilter = filters,
                AllowMultiple = false
            });

            if (files.Count > 0)
            {
                string selectedPath = files[0].Path.LocalPath;

                Status = L["Configuration.Fields.ValidatingImage"];

                var result = await ProfileImageService.SaveProfileImageAsync(selectedPath);

                Status = result.Message;
                if (result.Success)
                {
                    ProfileImagePath = result.Path;
                    HasProfileImage = true;
                }
            }
        }

        private async Task RemoveProfileImage()
        {
            var result = ProfileImageService.DeleteProfileImage();
            Status = L["Configuration.Fields.ProfileImageRemoved"];

            if (result.Success)
            {
                ProfileImagePath = null;
                HasProfileImage = false;
            }
        }
        
        private void LoadProfileImageBitmap()
        {
            if (!string.IsNullOrEmpty(ProfileImagePath) && File.Exists(ProfileImagePath))
            {
                try
                {
                    using var original = new Bitmap(ProfileImagePath);

                    var scaled = new RenderTargetBitmap(new PixelSize(100, 100), new Vector(96, 96));

                    using (var ctx = scaled.CreateDrawingContext())
                    {
                        ctx.DrawImage(
                            original,
                            new Rect(0, 0, original.PixelSize.Width, original.PixelSize.Height),
                            new Rect(0, 0, 100, 100)
                        );
                    }

                    ProfileImageBitmap = scaled;
                    HasProfileImage = true;
                }
                catch
                {
                    ProfileImageBitmap = null;
                    HasProfileImage = false;
                }
            }
            else
            {
                ProfileImageBitmap = null;
                HasProfileImage = false;
            }
        }

        private void SaveConfig()
        {
            _config.Language = IdiomaSeleccionado;
            _config.EnableSounds = EnableSounds;
            _config.EnableNotifications = EnableNotifications;
            _config.UseCustomIcon = UseCustomIcon;
            _config.HideRemainingTime = HideRemainingTime;
            _config.SelectedColor = SelectedColor;
            _config.ProfileImagePath = ProfileImagePath;
            _config.PreferredDownloadFolder = PreferredDownloadFolder;
            // === Guardar cookies ===
            _config.UseCookiesFile = UseCookiesFile;
            _config.CookiesFilePath = CookiesFilePath;
            _config.CookiesLastUpdated = CookiesLastUpdated;
            // Log activado/desactivado
            _config.Log = Log;
            _config.UseDarkTheme = UseDarkTheme;
            ConfigurationService.Save(_config);
        }
    }
}