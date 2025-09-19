using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Reactive;
using System.Threading.Tasks;
using ReactiveUI;
using VManager.Services;

namespace VManager.ViewModels
{
    public interface ICodecViewModel : INotifyPropertyChanged
    {
        string Status { get; }
        string Warning { get; }
        int Progress { get; }
        string OutputPath { get; }
        string VideoPath { get; }
        bool IsFileReadyVisible { get; }
        double GridWidth { set; }
        double HeightBlock { set; }
        ObservableCollection<string> AvailableVideoCodecs { get; }
        ObservableCollection<string> AvailableAudioCodecs { get; }
        ObservableCollection<VideoFormat> SupportedFormats { get; }
        string SelectedVideoCodec { get; } 
        string SelectedAudioCodec { get; }
        Task ReloadCodecsAsync();
        Task LoadOrRefreshCodecsAsync(Func<Task<CodecCache>> getCacheFunc);
        Task LoadCodecsAsync();
        
    }

    public class VideoFormat
    {
        public string Extension { get; set; }
        public string DisplayName { get; set; }
        public override string ToString() => DisplayName;
    }
}