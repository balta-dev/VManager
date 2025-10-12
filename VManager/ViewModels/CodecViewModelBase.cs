using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using ReactiveUI;
using VManager.Services;

namespace VManager.ViewModels
{
    public abstract class CodecViewModelBase : ViewModelBase, ICodecViewModel
    {
        private double _gridWidth = 511;
        private double _heightBlock = 300;
        public string _selectedVideoCodec = "";
        public string _selectedAudioCodec = "";
        public List<string> _allVideoCodecs = new();
        public List<string> _allAudioCodecs = new();
        public ObservableCollection<string> AvailableVideoCodecs { get; } = new();
        public ObservableCollection<string> AvailableAudioCodecs { get; } = new();
        public ObservableCollection<VideoFormat> SupportedVideoFormats { get; } = new ObservableCollection<VideoFormat>
        {
            new VideoFormat { Extension = "mp4", DisplayName = ".mp4" },
            new VideoFormat { Extension = "mkv", DisplayName = ".mkv" },
            new VideoFormat { Extension = "avi", DisplayName = ".avi" },
            new VideoFormat { Extension = "mov", DisplayName = ".mov" },
            new VideoFormat { Extension = "webm", DisplayName = ".webm" },
            new VideoFormat { Extension = "wmv", DisplayName = ".wmv" },
            new VideoFormat { Extension = "flv", DisplayName = ".flv" },
            new VideoFormat { Extension = "3gp", DisplayName = ".3gp" }
        };
        
        public ObservableCollection<AudioFormat> SupportedAudioFormats { get; } = new ObservableCollection<AudioFormat>
        {
            new AudioFormat { Extension = "libmp3lame", DisplayName = ".mp3" },
            new AudioFormat { Extension = "aac", DisplayName = ".aac" },
            new AudioFormat { Extension = "flac", DisplayName = ".flac" },
            new AudioFormat { Extension = "libopus", DisplayName = ".ogg (OPUS)" },
            new AudioFormat { Extension = "libvorbis", DisplayName = ".ogg (VORBIS)" }
        };

        public string SelectedVideoCodec
        {
            get => _selectedVideoCodec;
            set => this.RaiseAndSetIfChanged(ref _selectedVideoCodec, value);
        }

        public string SelectedAudioCodec
        {
            get => _selectedAudioCodec;
            set => this.RaiseAndSetIfChanged(ref _selectedAudioCodec, value);
        }

        public double GridWidth
        {
            get => _gridWidth;
            set => this.RaiseAndSetIfChanged(ref _gridWidth, value);
        }

        public double HeightBlock
        {
            get => _heightBlock;
            set => this.RaiseAndSetIfChanged(ref _heightBlock, value);
        }

        public async Task LoadOrRefreshCodecsAsync(Func<Task<CodecCache>> getCacheFunc)
        {
            // Inicializo con defaults conocidos para mostrar en la UI
            AvailableVideoCodecs.Clear();
            AvailableVideoCodecs.Add("libx264");
            AvailableAudioCodecs.Clear();
            AvailableAudioCodecs.Add("aac");

            // Selecciono inmediatamente
            SelectedVideoCodec = "libx264";
            SelectedAudioCodec = "aac";
            
            var cache = await getCacheFunc.Invoke();
            
            _allVideoCodecs = cache.VideoCodecs.ToList();
            _allAudioCodecs = cache.AudioCodecs.ToList();

            AvailableVideoCodecs.Clear();
            foreach (var v in _allVideoCodecs)
                AvailableVideoCodecs.Add(v);

            AvailableAudioCodecs.Clear();
            foreach (var a in _allAudioCodecs)
                AvailableAudioCodecs.Add(a);

            SelectedVideoCodec = AvailableVideoCodecs.FirstOrDefault() ?? "libx264";
            SelectedAudioCodec = AvailableAudioCodecs.Contains("aac")
                ? "aac"
                : AvailableAudioCodecs.FirstOrDefault() ?? "aac";
            
        }
        public async Task LoadCodecsAsync()
        {
            
            Console.WriteLine("Cargando códecs para la herramienta...");
            await LoadOrRefreshCodecsAsync(async () =>
            {
                var codecService = new CodecService();
                var cacheService = new CodecCacheService(codecService);
                return await cacheService.LoadOrBuildCacheAsync();
            });

        }

        public async Task ReloadCodecsAsync()
        {
            Console.WriteLine("Recargando códecs...");
            await LoadOrRefreshCodecsAsync(async () =>
            {
                var codecService = new CodecService();
                var cacheService = new CodecCacheService(codecService);
                return await cacheService.RefreshCacheAsync();
            });
        }
    }
}