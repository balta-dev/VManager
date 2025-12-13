using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Reactive;
using System.Reactive.Linq;
using System.Runtime.InteropServices;
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
using VManager.Views;

namespace VManager.ViewModels
{
    public class AcercaDeViewModel : ViewModelBase, INotifyPropertyChanged
    {
        private bool isVideoPathSet;
        public override bool IsVideoPathSet
        {
            get => isVideoPathSet;
            set => this.RaiseAndSetIfChanged(ref isVideoPathSet, value);
        }
        
        public bool IsWindows { get; } =
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        public bool IsLinuxMac { get; } =
            RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ||
            RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        
        public event PropertyChangedEventHandler? PropertyChanged;
        public LocalizationService L => LocalizationService.Instance;

        public AcercaDeViewModel()
        {
            LocalizationService.Instance.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "Item[]" || e.PropertyName == "CurrentLanguage")
                {
                    // Notifica que L "cambió" para que las vistas se refresquen
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(L)));
                }
            };
        }
    }
}