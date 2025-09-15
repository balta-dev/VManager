using Avalonia.Controls;
using Avalonia.ReactiveUI;
using FFMpegCore;
using FFMpegCore.Enums;
using ReactiveUI;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using VManager.Services;

namespace VManager.ViewModels
{
    public class Herramienta3ViewModel : ViewModelBase
    {
        
        // Listas de codecs
        public ObservableCollection<string> AvailableVideoCodecs { get; } = new();
        public ObservableCollection<string> AvailableAudioCodecs { get; } = new();
        
        public class VideoFormat
        {
            public string Extension { get; set; }
            public string DisplayName { get; set; }
    
            public override string ToString() => DisplayName;
        }

        public ObservableCollection<VideoFormat> SupportedFormats { get; set; } = new ObservableCollection<VideoFormat>
        {
            new VideoFormat { Extension = "mp4", DisplayName = "MP4" },
            new VideoFormat { Extension = "mkv", DisplayName = "MKV" },
            new VideoFormat { Extension = "avi", DisplayName = "AVI" },
            new VideoFormat { Extension = "mov", DisplayName = "MOV" },
            new VideoFormat { Extension = "webm", DisplayName = "WebM" },
            new VideoFormat { Extension = "wmv", DisplayName = "WMV" },
            new VideoFormat { Extension = "flv", DisplayName = "FLV" },
            new VideoFormat { Extension = "3gp", DisplayName = "3GP" }
        };

        public VideoFormat SelectedFormat { get; set; } 

        // Selected
        private string _selectedVideoCodec = null;
        public string SelectedVideoCodec
        {
            get => _selectedVideoCodec;
            set => this.RaiseAndSetIfChanged(ref _selectedVideoCodec, value);
        }

        private string _selectedAudioCodec = null;
        public string SelectedAudioCodec
        {
            get => _selectedAudioCodec;
            set => this.RaiseAndSetIfChanged(ref _selectedAudioCodec, value);
        }
        
        public ReactiveCommand<Unit, Unit> ConvertCommand { get; }
        public ReactiveCommand<Unit, Unit> RefreshCodecsCommand { get; } 
        
        public Herramienta3ViewModel()
        {
            RefreshCodecsCommand = ReactiveCommand.CreateFromTask(ReloadCodecsAsync, outputScheduler: AvaloniaScheduler.Instance);
            ConvertCommand = ReactiveCommand.CreateFromTask(ConvertVideo, outputScheduler: AvaloniaScheduler.Instance);
            _ = LoadCodecsAsync();
            SelectedFormat = SupportedFormats[0];
        }

        private async Task LoadOrRefreshCodecsAsync(Func<Task<CodecCache>> getCacheFunc)
        {
            var codecService = new CodecService();
            var cacheService = new CodecCacheService(codecService);

            var cache = await getCacheFunc.Invoke();

            AvailableVideoCodecs.Clear();
            foreach (var v in cache.VideoCodecs)
                AvailableVideoCodecs.Add(v);

            AvailableAudioCodecs.Clear();
            foreach (var a in cache.AudioCodecs)
                AvailableAudioCodecs.Add(a);

            SelectedVideoCodec = AvailableVideoCodecs.FirstOrDefault() ?? "libx264";
            SelectedAudioCodec = AvailableAudioCodecs.Contains("aac")
                ? "aac"
                : AvailableAudioCodecs.FirstOrDefault();

            Console.WriteLine($"Hardware detectado: {cache.Hardware}");
        }
        
        private Task LoadCodecsAsync()
        {
            return LoadOrRefreshCodecsAsync(async () =>
            {
                var codecService = new CodecService();
                var cacheService = new CodecCacheService(codecService);
                return await cacheService.LoadOrBuildCacheAsync();
            });
        }
        
        private Task ReloadCodecsAsync()
        {
            return LoadOrRefreshCodecsAsync(async () =>
            {
                var codecService = new CodecService();
                var cacheService = new CodecCacheService(codecService);
                return await cacheService.RefreshCacheAsync();
            });
        }
        
        private async Task ConvertVideo()
        {
            HideFileReadyButton();
            
            Status = "Obteniendo información del video...";
            this.RaisePropertyChanged(nameof(Status));

            var processor = new VideoProcessor();
            var progress = new Progress<double>(p =>
            {
                Progress = (int)(p * 100);
                this.RaisePropertyChanged(nameof(Progress));
            });

            Status = "Convirtiendo...";
            this.RaisePropertyChanged(nameof(Status));
            
            var result = await processor.ConvertAsync(
                VideoPath,
                OutputPath,
                SelectedVideoCodec,
                SelectedAudioCodec,
                SelectedFormat?.Extension,
                progress
            );
            
            if (result.Success)
            {
                SoundManager.Play("success.wav");
                OutputPath = result.OutputPath;
                SetLastCompressedFile(OutputPath);
                Status = result.Message;
                Warning = result.Warning;
                Progress = 100;
                SoundManager.Play("success.wav");
                SetLastCompressedFile(result.OutputPath);
                Status = result.Message;
                Warning = result.Warning;
                Progress = 100;
                OutputPath = "Archivo: " + result.OutputPath;
                this.RaisePropertyChanged(nameof(Status));
                this.RaisePropertyChanged(nameof(Progress));
                this.RaisePropertyChanged(nameof(OutputPath));
                this.RaisePropertyChanged(nameof(Warning));
            }
            else
            {
                SoundManager.Play("fail.wav");
                Status = result.Message; 
                Progress = 0;
                this.RaisePropertyChanged(nameof(Status));
                this.RaisePropertyChanged(nameof(Progress));
            }
            
        }
        
    }
    
}