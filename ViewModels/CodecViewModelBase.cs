using System;
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
        private string _selectedVideoCodec = "";
        private string _selectedAudioCodec = "";

        public ObservableCollection<string> AvailableVideoCodecs { get; } = new();
        public ObservableCollection<string> AvailableAudioCodecs { get; } = new();
        public ObservableCollection<VideoFormat> SupportedFormats { get; } = new ObservableCollection<VideoFormat>
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