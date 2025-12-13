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
        
        private VideoDownloadItem? _selectedVideo;
        public VideoDownloadItem? SelectedVideo
        {
            get => _selectedVideo;
            set => this.RaiseAndSetIfChanged(ref _selectedVideo, value);
        }
        
        private bool _showDownloadHelp;
        public bool ShowDownloadHelp
        {
            get => _showDownloadHelp;
            set => this.RaiseAndSetIfChanged(ref _showDownloadHelp, value);
        }
        
        public override void ClearInfo()
        {
            // Primero ejecuta el ClearInfo del base
            base.ClearInfo();
            
            SelectedVideo = null;
            this.RaisePropertyChanged(nameof(SelectedVideo));
            
        }
        
        private readonly ConfigurationService.AppConfig _config;

        public ReactiveCommand<Unit, Unit> DownloadCommand { get; }
        public ReactiveCommand<string, Unit> AddUrlCommand { get; }
        public ReactiveCommand<VideoDownloadItem, Unit> RemoveUrlCommand { get; }
        public ReactiveCommand<Unit, Unit> HideDownloadHelpCommand { get; }

        public Herramienta5ViewModel()
        {
            _config = ConfigurationService.Load();
            DownloadCommand = ReactiveCommand.CreateFromTask(DownloadVideos, outputScheduler: AvaloniaScheduler.Instance);
            AddUrlCommand = ReactiveCommand.Create<string>(AddUrl, outputScheduler: AvaloniaScheduler.Instance);
            RemoveUrlCommand = ReactiveCommand.Create<VideoDownloadItem>(RemoveUrl, outputScheduler: AvaloniaScheduler.Instance);
            HideDownloadHelpCommand = ReactiveCommand.Create(
                () => { ShowDownloadHelp = false; },
                outputScheduler: AvaloniaScheduler.Instance
            );
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
                Status = L["VideoStatus.NotURL"];
                this.RaisePropertyChanged(nameof(Status));
                return;
            }

            // Verificar si ya existe
            if (Videos.Any(v => v.Url == url))
            {
                Status = L["VideoStatus.AlreadyOnList"];
                this.RaisePropertyChanged(nameof(Status));
                return;
            }

            // Crear item temporal
            var videoItem = new VideoDownloadItem
            {
                Url = url,
                Title = L["VideoStatus.RetrievingInfo"],
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
                    videoItem.ErrorMessage = L["VideoStatus.ErrorNoInfo"];
                    videoItem.Title = L["VideoStatus.ErrorNoInfo"];
                    videoItem.IsLoading = false;
                    ShowDownloadHelp = true;
                    return;
                }

                // Actualizar info
                videoItem.Title = info.Title;
                videoItem.Duration = FormatDuration(info.Duration);
                videoItem.ThumbnailUrl = info.Thumbnail;
                videoItem.FileSize = info.FileSize;
                
                // Cargar formatos disponibles - CON AUDIO
                var seenResolutions = new HashSet<string>();
                var formatList = new List<VManager.Models.VideoFormat>();

                // 1. Formatos de VIDEO+AUDIO
                foreach (var f in info.Formats
                             .Where(fmt => !string.IsNullOrEmpty(fmt.VideoCodec) && 
                                           fmt.VideoCodec != "none" && 
                                           !string.IsNullOrEmpty(fmt.AudioCodec) && 
                                           fmt.AudioCodec != "none" && 
                                           fmt.Height.HasValue)
                             .OrderByDescending(fmt => fmt.Height))
                {
                    string resolution = $"{f.Height}p";
    
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
                
                //////////////////////// 2. Formatos de SOLO AUDIO (NO FUNCIONA) ////////////////////////////////
                //var seenAudioFormats = new HashSet<string>();
                //foreach (var f in info.Formats
                //             .Where(fmt => fmt.VideoCodec != null && fmt.VideoCodec.Equals("none", StringComparison.OrdinalIgnoreCase))
                //             .Where(fmt => !string.IsNullOrEmpty(fmt.AudioCodec) && !fmt.AudioCodec.Equals("none", StringComparison.OrdinalIgnoreCase))
                //             .OrderByDescending(fmt => fmt.Abr ?? 0))
                //{
                //    string audioDesc = f.Abr.HasValue 
                //        ? $"Audio {Math.Round(f.Abr.Value)}kbps" 
                //        : "Audio";
                //    
                //    string uniqueKey = $"{audioDesc}_{f.Extension}";
    
                //    if (seenAudioFormats.Add(uniqueKey))
                //    {
                //        formatList.Add(new VManager.Models.VideoFormat
                //        {
                //            FormatId = f.FormatId,
                //            Resolution = audioDesc,
                //            Extension = f.Extension,
                //            FileSize = f.FileSize
                //        });
                //    }
                //}
                /////////////////////////////////////////////////////////////////////////////////////////////
                
                formatList.Add(new VManager.Models.VideoFormat
                {
                    FormatId = "0",
                    Resolution = "audio",
                    Extension = "mp3",
                    FileSize = null
                });
                
                formatList.Add(new VManager.Models.VideoFormat
                {
                    FormatId = "1",
                    Resolution = "audio",
                    Extension = "wav",
                    FileSize = null
                });
                
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
                videoItem.Title = L["VideoStatus.ErrorNoInfo"];
                videoItem.IsLoading = false;
                Console.WriteLine($"Error cargando info: {ex}");
            }
        }

        private string FormatDuration(double seconds)
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
                Status = L["VideoStatus.CantDeleteWhileDownloading"];
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
                Status = L["VideoStatus.NoVideo"];
                this.RaisePropertyChanged(nameof(Status));
                return;
            }

            HideFileReadyButton();
            _cts = new CancellationTokenSource();
            Progress = 0;
            this.RaisePropertyChanged(nameof(Progress));

            try
            {
                var processor = new YtDlpProcessor();

                IsConverting = true;
                IsOperationRunning = true;

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

                        Status = $"{L["VideoStatus.Downloading"]} {index}/{total}: {video.Title}";
                        this.RaisePropertyChanged(nameof(Progress));
                        this.RaisePropertyChanged(nameof(RemainingTime));
                        this.RaisePropertyChanged(nameof(Status));
                    });

                    // Guardar en carpeta preferida
                    string downloadFolder = !string.IsNullOrWhiteSpace(_config.PreferredDownloadFolder)
                        ? _config.PreferredDownloadFolder
                        : Environment.GetFolderPath(Environment.SpecialFolder.Desktop); // fallback seguro

                    string extension = video.SelectedFormat?.FormatId switch
                    {
                        "0" => "mp3",
                        "1" => "wav",
                        _ => "mp4"
                    };

                    string outputTemplate = Path.Combine(
                        downloadFolder,
                        $"%(title)s.{extension}"
                    );

                    var result = await processor.DownloadAsync(
                        video.Url,
                        outputTemplate,
                        progress,
                        _cts.Token,
                        video.SelectedFormat?.FormatId
                    );

                    video.IsDownloading = false;

                    if (result.Success)
                    {
                        success++;
                        video.IsCompleted = true;
                        video.Status = L["VideoStatus.Completed"];
                        video.Progress = 100;

                        var notifier = new Notifier();
                        notifier.ShowFileConvertedNotification("Descargado", outputTemplate);

                        _ = SoundManager.Play("success.wav");
                        SetLastCompressedFile(outputTemplate);
                    }
                    else
                    {
                        if (_cts.IsCancellationRequested)
                        {
                            video.IsCanceled = true;
                            video.Status = L["VideoStatus.Canceled"];
                            video.Progress = 0;
                            Status = $"{L["VideoStatus.Canceled"]} {success}/{total}";
                        }
                        else
                        {
                            video.HasError = true;
                            video.Status = L["VideoStatus.Error"];
                            _ = SoundManager.Play("fail.wav");
                            Status = $"{L["VideoStatus.Error"]} {video.Title}: {result.Message}";
                        }

                        this.RaisePropertyChanged(nameof(Status));
                    }
                }

                Progress = 100;
                Status = $"{L["VideoStatus.Completed"]} {success}/{total} {L["VideoStatus.Downloads"]}";
            }
            catch (OperationCanceledException)
            {
                _ = SoundManager.Play("fail.wav");
                Status = L["VideoStatus.OperationCanceled"];
                Progress = 0;
            }
            finally
            {
                IsConverting = false;
                IsOperationRunning = false;

                _cts?.Dispose();
                _cts = null;
                this.RaisePropertyChanged(nameof(Status));
                this.RaisePropertyChanged(nameof(Progress));
                this.RaisePropertyChanged(nameof(IsConverting));
                this.RaisePropertyChanged(nameof(IsOperationRunning));
            }
        }
    }
}