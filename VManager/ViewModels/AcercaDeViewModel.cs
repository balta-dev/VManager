using System.ComponentModel;
using System.Runtime.InteropServices;
using ReactiveUI;
using VManager.Services;
using VManager.Services.Core;

namespace VManager.ViewModels
{
    public class AcercaDeViewModel : ViewModelBase, INotifyPropertyChanged
    {
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