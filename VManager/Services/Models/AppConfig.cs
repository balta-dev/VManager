using System;
using System.Text.Json.Serialization;
using Avalonia.Media;
using ReactiveUI;
using VManager.Services.Core.Converters;

namespace VManager.Services.Models;

public class AppConfig : ReactiveObject
    {
        private string _language = "Español";
        public string Language
        {
            get => _language;
            set => this.RaiseAndSetIfChanged(ref _language, value);
        }

        private bool _enableSounds = true;
        public bool EnableSounds
        {
            get => _enableSounds;
            set => this.RaiseAndSetIfChanged(ref _enableSounds, value);
        }

        private bool _enableNotifications = true;
        public bool EnableNotifications
        {
            get => _enableNotifications;
            set => this.RaiseAndSetIfChanged(ref _enableNotifications, value);
        }

        private bool _useCustomIcon;
        public bool UseCustomIcon
        {
            get => _useCustomIcon;
            set => this.RaiseAndSetIfChanged(ref _useCustomIcon, value);
        }

        private bool _hideRemainingTime;
        public bool HideRemainingTime
        {
            get => _hideRemainingTime;
            set => this.RaiseAndSetIfChanged(ref _hideRemainingTime, value);
        }

        private Color? _selectedColor;
        [JsonConverter(typeof(ColorJsonConverter))]
        public Color? SelectedColor
        {
            get => _selectedColor;
            set => this.RaiseAndSetIfChanged(ref _selectedColor, value);
        }

        private string? _profileImagePath;
        public string? ProfileImagePath
        {
            get => _profileImagePath;
            set => this.RaiseAndSetIfChanged(ref _profileImagePath, value);
        }

        private string? _preferredDownloadFolder;
        public string? PreferredDownloadFolder
        {
            get => _preferredDownloadFolder;
            set => this.RaiseAndSetIfChanged(ref _preferredDownloadFolder, value);
        }

        // === Cookies ===
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
            set => this.RaiseAndSetIfChanged(ref _cookiesLastUpdated, value);
        }
        
        // === Log ===
        private bool _log;
        public bool Log
        {
            get => _log;
            set => this.RaiseAndSetIfChanged(ref _log, value);
        }
        
        public bool? UseDarkTheme { get; set; } = true; // o false según tu default
        
        private bool _useCustomDecorations = false;
        public bool UseCustomDecorations
        {
            get => _useCustomDecorations;
            set => this.RaiseAndSetIfChanged(ref _useCustomDecorations, value);
        }
        
        private bool _hidePane = false;
        public bool HidePane
        {
            get => _hidePane;
            set => this.RaiseAndSetIfChanged(ref _hidePane, value);
        }
        
        public bool ShowThemeToggleButton { get; set; }
        
        public string? ThemeName { get; set; }
    }