using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.ReactiveUI;
using ReactiveUI;
using VManager.Models; // <-- Necesitas crear la carpeta Models
using VManager.Services;

namespace VManager.ViewModels
{
    public class Herramienta5ViewModel : CodecViewModelBase
    {
        // Nueva propiedad para la colección de videos con info
        private ObservableCollection<VideoDownloadItem> _videos = new();
        public ObservableCollection<VideoDownloadItem> Videos
        {
            get => _videos;
            set => this.RaiseAndSetIfChanged(ref _videos, value);
        }

        private string _urlText = string.Empty;
        public string UrlText
        {
            get => _urlText;
            set => this.RaiseAndSetIfChanged(ref _urlText, value);
        }

        private readonly HttpClient _httpClient = new();

        public AudioFormat SelectedAudioFormat { get; set; }
        
        private bool _isConverting;
        public bool IsConverting
        {
            get => _isConverting;
            set => this.RaiseAndSetIfChanged(ref _isConverting, value);
        }
        
        private readonly ConfigurationService.AppConfig _config;

        public ReactiveCommand<Unit, Unit> DownloadCommand { get; }
        public ReactiveCommand<string, Unit> AddUrlCommand { get; }
        public ReactiveCommand<VideoDownloadItem, Unit> RemoveUrlCommand { get; }

        public Herramienta5ViewModel()
        {
            _config = ConfigurationService.Load();
            DownloadCommand = ReactiveCommand.CreateFromTask(DownloadVideos, outputScheduler: AvaloniaScheduler.Instance);
            AddUrlCommand = ReactiveCommand.Create<string>(AddUrl, outputScheduler: AvaloniaScheduler.Instance);
            RemoveUrlCommand = ReactiveCommand.Create<VideoDownloadItem>(RemoveUrl, outputScheduler: AvaloniaScheduler.Instance);
            SelectedAudioFormat = SupportedAudioFormats[0]; // mp3
        }

        public override bool IsVideoPathSet
        {
            get => Videos.Count > 0;
            set { /* requerido por la clase base */ }
        }

        public int VideoCount => Videos.Count;

        private bool IsValidUrl(string url)
        {
            return Uri.TryCreate(url, UriKind.Absolute, out var uri)
                   && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        // Agregar URL y cargar su info
        public async void AddUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return;

            if (!IsValidUrl(url))
            {
                Status = "URL inválida.";
                this.RaisePropertyChanged(nameof(Status));
                return;
            }

            // Verificar si ya existe
            if (Videos.Any(v => v.Url == url))
            {
                Status = "Este video ya está en la lista.";
                this.RaisePropertyChanged(nameof(Status));
                return;
            }

            // Crear item temporal
            var videoItem = new VideoDownloadItem
            {
                Url = url,
                Title = "Obteniendo información...",
                IsLoading = true
            };

            Videos.Add(videoItem);
            UrlText = string.Empty; // Limpiar textbox
            
            this.RaisePropertyChanged(nameof(IsVideoPathSet));
            this.RaisePropertyChanged(nameof(VideoCount));

            // Cargar metadata en background
            await LoadVideoInfoAsync(videoItem);
        }

        private async Task LoadVideoInfoAsync(VideoDownloadItem videoItem)
        {
            try
            {
                var processor = new YtDlpProcessor();
                var info = await processor.GetVideoInfoAsync(videoItem.Url);

                if (info == null)
                {
                    videoItem.HasError = true;
                    videoItem.ErrorMessage = "No se pudo obtener información";
                    videoItem.Title = "Error al cargar video";
                    videoItem.IsLoading = false;
                    return;
                }

                // Actualizar info
                videoItem.Title = info.Title;
                videoItem.Duration = FormatDuration(info.Duration);
                videoItem.ThumbnailUrl = info.Thumbnail;
                videoItem.FileSize = info.FileSize;

                // Cargar formatos disponibles - VERSIÓN QUE FUNCIONA
                var seenResolutions = new System.Collections.Generic.HashSet<string>();
                var formatList = new System.Collections.Generic.List<VManager.Models.VideoFormat>();
                
                foreach (var f in info.Formats
                    .Where(fmt => !string.IsNullOrEmpty(fmt.VideoCodec) && 
                                 fmt.VideoCodec != "none" && 
                                 !string.IsNullOrEmpty(fmt.AudioCodec) && 
                                 fmt.AudioCodec != "none" && 
                                 fmt.Height.HasValue)
                    .OrderByDescending(fmt => fmt.Height))
                {
                    string resolution = $"{f.Height}p";
                    
                    // Solo agregar si no hemos visto esta resolución
                    if (seenResolutions.Add(resolution))
                    {
                        formatList.Add(new VManager.Models.VideoFormat
                        {
                            FormatId = f.FormatId,
                            Resolution = resolution,
                            Extension = f.Extension,
                            FileSize = f.FileSize
                        });
                    }
                }
                
                videoItem.AvailableFormats = new ObservableCollection<VManager.Models.VideoFormat>(formatList);

                // Seleccionar formato por defecto
                videoItem.SelectedFormat = videoItem.AvailableFormats.FirstOrDefault();

                // Descargar thumbnail
                if (!string.IsNullOrEmpty(info.Thumbnail))
                {
                    try
                    {
                        var thumbnailBytes = await _httpClient.GetByteArrayAsync(info.Thumbnail);
                        using var ms = new MemoryStream(thumbnailBytes);
                        videoItem.ThumbnailBitmap = new Bitmap(ms);
                    }
                    catch
                    {
                        // Si falla el thumbnail, no es crítico
                    }
                }

                videoItem.IsLoading = false;
            }
            catch (Exception ex)
            {
                videoItem.HasError = true;
                videoItem.ErrorMessage = ex.Message;
                videoItem.Title = "Error al cargar información";
                videoItem.IsLoading = false;
                Console.WriteLine($"Error cargando info: {ex}");
            }
        }

        private string FormatDuration(int seconds)
        {
            var ts = TimeSpan.FromSeconds(seconds);
            if (ts.TotalHours >= 1)
                return ts.ToString(@"h\:mm\:ss");
            return ts.ToString(@"m\:ss");
        }

        public void RemoveUrl(VideoDownloadItem video)
        {
            if (video.IsDownloading)
            {
                Status = "No se puede eliminar un video mientras se está descargando.";
                this.RaisePropertyChanged(nameof(Status));
                return;
            }
            
            video.IsCanceled = true;
            Videos.Remove(video);
            this.RaisePropertyChanged(nameof(IsVideoPathSet));
            this.RaisePropertyChanged(nameof(VideoCount));
        }

        protected override bool AllowAudioFiles => false;
        
        private readonly Dictionary<VideoDownloadItem, double> _maxProgress = new();
        
        private readonly Dictionary<VideoDownloadItem, double> _realProgress = new();

        private async Task DownloadVideos()
        {
            if (Videos.Count == 0)
            {
                Status = "No hay videos para descargar.";
                this.RaisePropertyChanged(nameof(Status));
                return;
            }

            HideFileReadyButton();
            _cts = new CancellationTokenSource();

            try
            {
                var processor = new YtDlpProcessor();

                IsConverting = true;
                IsOperationRunning = true;
                Progress = 0;

                int total = Videos.Count;
                int index = 0;
                int success = 0;

                foreach (var video in Videos.Where(v => !v.IsCompleted).ToList())
                {
                    index++;
                    if (video.IsCanceled)
                        continue;
                    
                    video.IsDownloading = true;

                    var progress = new Progress<YtDlpProcessor.YtDlpProgress>(p =>
                    {
                        double newValue = p.Progress * 100;

                        if (!_maxProgress.ContainsKey(video))
                            _maxProgress[video] = 0;

                        // Ignorar spikes tempranos de 100%
                        if (newValue >= 100 && _maxProgress[video] < 5)
                            return;

                        // Solo avanzar
                        if (newValue < _maxProgress[video])
                            newValue = _maxProgress[video];
                        else
                            _maxProgress[video] = newValue;

                        video.Progress = newValue;
                        video.Status = $"{p.Speed} - ETA: {p.Eta}";

                        // Actualizar progreso individual en global
                        _realProgress[video] = newValue;

                        // Calcular porcentaje global
                        double globalProgress = Videos.Count > 0 
                            ? Videos.Average(v => _realProgress.ContainsKey(v) ? _realProgress[v] : 0) 
                            : 0;

                        Progress = (int)globalProgress;
                        RemainingTime = p.Eta;

                        Status = $"Descargando {index}/{total}: {video.Title}";
                        this.RaisePropertyChanged(nameof(Progress));
                        this.RaisePropertyChanged(nameof(RemainingTime));
                        this.RaisePropertyChanged(nameof(Status));
                    });

                    // Guardar en carpeta preferida
                    string downloadFolder = !string.IsNullOrWhiteSpace(_config.PreferredDownloadFolder)
                        ? _config.PreferredDownloadFolder
                        : Environment.GetFolderPath(Environment.SpecialFolder.Desktop); // fallback seguro

                    string outputTemplate = Path.Combine(
                        downloadFolder,
                        $"%(title)s.mp4"
                    );

                    var result = await processor.DownloadAsync(
                        video.Url,
                        outputTemplate,
                        progress,
                        _cts.Token
                    );

                    video.IsDownloading = false;

                    if (result.Success)
                    {
                        success++;
                        video.IsCompleted = true;
                        video.Status = "✓ Completado";
                        video.Progress = 100;

                        var notifier = new Notifier();
                        notifier.ShowFileConvertedNotification("Descargado", result.OutputPath);

                        _ = SoundManager.Play("success.wav");
                        SetLastCompressedFile(result.OutputPath);
                    }
                    else
                    {
                        video.HasError = true;
                        video.Status = "✗ Error";
                        _ = SoundManager.Play("fail.wav");
                        Status = $"Error en {video.Title}: {result.Message}";
                        this.RaisePropertyChanged(nameof(Status));
                    }
                }

                Progress = 100;
                Status = $"Completado {success}/{total} descargas";
                this.RaisePropertyChanged(nameof(Status));
            }
            catch (OperationCanceledException)
            {
                _ = SoundManager.Play("fail.wav");
                Status = "Operación cancelada.";
                Progress = 0;
                this.RaisePropertyChanged(nameof(Status));
            }
            finally
            {
                IsConverting = false;
                IsOperationRunning = false;

                _cts?.Dispose();
                _cts = null;

                this.RaisePropertyChanged(nameof(IsConverting));
                this.RaisePropertyChanged(nameof(IsOperationRunning));
            }
        }
    }
}