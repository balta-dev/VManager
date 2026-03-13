using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.ReactiveUI;
using ReactiveUI;
using VManager.Models;
using VManager.Services;
using VManager.Services.Core;
using VManager.Services.Models;

namespace VManager.ViewModels.Herramientas
{
    [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicProperties)]
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
        
        private readonly AppConfig _config;

        public ReactiveCommand<Unit, Unit> DownloadCommand { get; }
        public ReactiveCommand<string, Unit> AddUrlCommand { get; }
        public ReactiveCommand<VideoDownloadItem, Unit> RemoveUrlCommand { get; }
        public ReactiveCommand<Unit, Unit> HideDownloadHelpCommand { get; }
        
        private readonly SemaphoreSlim _downloadSemaphore = new SemaphoreSlim(3, 3); // Máx 3 simultáneas (ajustá según tu máquina/red)

        public Herramienta5ViewModel()
        {
            _config = ConfigurationService.Current;
            DownloadCommand = ReactiveCommand.CreateFromTask(DownloadVideos, outputScheduler: AvaloniaScheduler.Instance);
            AddUrlCommand = ReactiveCommand.Create<string>(AddUrl, outputScheduler: AvaloniaScheduler.Instance);
            RemoveUrlCommand = ReactiveCommand.Create<VideoDownloadItem>(RemoveUrl, outputScheduler: AvaloniaScheduler.Instance);
            HideDownloadHelpCommand = ReactiveCommand.Create(
                () => { ShowDownloadHelp = false; },
                outputScheduler: AvaloniaScheduler.Instance
            );
            SelectedAudioFormat = SupportedAudioFormats[0]; // mp3
        }

        public bool IsVideoPathSet
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
                var (info, cookiesProblem, usedCookies) =
                    await processor.GetVideoInfoWithDetectionAsync(videoItem.Url);
                
                videoItem.UsedCookies = usedCookies;

                if (cookiesProblem)
                {
                    videoItem.HasError = true;
                    videoItem.Title = L["VideoStatus.ErrorNoInfo"];
                    videoItem.IsLoading = false;
                    ShowDownloadHelp = true;
                    ErrorService.Show("Su archivo de cookies caducó. Por favor, renuévelo o quítelo.", null, "Advertencia", "#FFFFA500");
                    // return; dejar al usuario seguir a pesar de eso.
                }

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
                ErrorService.Show(ex);
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

                var pendingVideos = Videos.Where(v => !v.IsCompleted && !v.IsCanceled).ToList();
                int total = pendingVideos.Count;

                if (total == 0)
                {
                    Status = L["VideoStatus.NoVideo"];
                    this.RaisePropertyChanged(nameof(Status));
                    return;
                }

                int successCount = 0;
                int finishedCount = 0;
                var errorMessages = new List<string>();
                string lastGlobalETA = "";
                object lockObj = new object();

                // Reporte inicial
                Status = $"{L["VideoStatus.Completed"]} 0/{total} {L["VideoStatus.Downloads"]}";
                this.RaisePropertyChanged(nameof(Status));

                // Inicializar progresos
                foreach (var v in pendingVideos)
                {
                    _realProgress[v] = 0;
                    _maxProgress[v] = 0;
                }

                var downloadTasks = pendingVideos.Select(async (currentVideo) =>
                {
                    await _downloadSemaphore.WaitAsync(_cts.Token);

                    try
                    {
                        currentVideo.IsDownloading = true;
                        currentVideo.Progress = 0;

                        if (total == 1)
                        {
                            currentVideo.Status = L["VideoStatus.Downloading"];
                        }

                        var progress = new Progress<YtDlpProgress>(p =>
                        {
                            double newValue = p.Progress * 100;

                            lock (lockObj)
                            {
                                if (!_maxProgress.ContainsKey(currentVideo))
                                    _maxProgress[currentVideo] = 0;

                                if (newValue >= 100 && _maxProgress[currentVideo] < 5)
                                    return;

                                if (newValue < _maxProgress[currentVideo])
                                    newValue = _maxProgress[currentVideo];
                                else
                                    _maxProgress[currentVideo] = newValue;

                                _realProgress[currentVideo] = newValue;

                                // Guardar ETA global solo si NO es "Unknown"
                                if (!string.IsNullOrWhiteSpace(p.Eta) && 
                                    !p.Eta.Equals("Unknown", StringComparison.OrdinalIgnoreCase))
                                {
                                    lastGlobalETA = p.Eta;
                                }
                            }

                            currentVideo.Progress = newValue;

                            // Status detallado si es 1 solo video
                            if (total == 1)
                            {
                                currentVideo.Status = $"{p.Speed} - ETA: {p.Eta}";
                            }
                            else
                            {
                                // Status con porcentaje + ETA individual si hay múltiples videos
                                string etaPart = !string.IsNullOrWhiteSpace(p.Eta) ? $" - ETA: {p.Eta}" : "";
                                currentVideo.Status = $"{newValue:F0}%{etaPart}";
                            }

                            // Calcular progreso global
                            double globalProgress = pendingVideos.Count > 0
                                ? pendingVideos.Average(v => _realProgress.GetValueOrDefault(v, 0))
                                : 0;

                            Progress = (int)globalProgress;
                            RemainingTime = lastGlobalETA;

                            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                            {
                                this.RaisePropertyChanged(nameof(Progress));
                                this.RaisePropertyChanged(nameof(RemainingTime));
                                this.RaisePropertyChanged(nameof(Status));
                            });
                        });

                        string downloadFolder = !string.IsNullOrWhiteSpace(_config.PreferredDownloadFolder)
                            ? _config.PreferredDownloadFolder
                            : Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

                        string extension = currentVideo.SelectedFormat?.FormatId switch
                        {
                            "0" => "mp3",
                            "1" => "wav",
                            _ => "mp4"
                        };

                        string safeTitle = currentVideo.Title
                            .Replace("/", "-")
                            .Replace("\\", "-")
                            .Replace("\"", "'");
                        string outputTemplate = Path.Combine(downloadFolder, $"{safeTitle}.{extension}");

                        var result = await processor.DownloadAsync(
                            currentVideo.Url,
                            outputTemplate,
                            progress,
                            _cts.Token,
                            currentVideo.SelectedFormat?.FormatId,
                            currentVideo.UsedCookies
                        );

                        // Chequeo: archivo existe → éxito aunque result diga false
                        bool fileDownloaded = File.Exists(outputTemplate);

                        if (fileDownloaded || result.Success)
                        {
                            Interlocked.Increment(ref successCount);
                            currentVideo.IsCompleted = true;
                            currentVideo.Status = L["VideoStatus.Completed"];
                            currentVideo.Progress = 100;

                            var notifier = new NotificationService();
                            notifier.ShowFileConvertedNotification($"{L["VideoStatus.Downloaded"]} {currentVideo.Title}", outputTemplate);

                            _ = SoundManager.Play("success.wav");
                            SetLastCompressedFile(outputTemplate);
                        }
                        else
                        {
                            currentVideo.HasError = true;
                            currentVideo.Status = L["VideoStatus.Error"];
                            
                            lock (lockObj)
                            {
                                errorMessages.Add($"{currentVideo.Title}: {result.Message ?? "Error desconocido"}");
                            }
                            
                            _ = SoundManager.Play("fail.wav");
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        currentVideo.IsCanceled = true;
                        currentVideo.Status = L["VideoStatus.Canceled"];
                        currentVideo.Progress = 0;
                    }
                    catch (Exception ex)
                    {
                        currentVideo.HasError = true;
                        currentVideo.Status = L["VideoStatus.Error"];
                        
                        lock (lockObj)
                        {
                            errorMessages.Add($"{currentVideo.Title}: {ex.Message}");
                        }
                        
                        _ = SoundManager.Play("fail.wav");
                        ErrorService.Show(ex);
                    }
                    finally
                    {
                        currentVideo.IsDownloading = false;
                        _downloadSemaphore.Release();

                        Interlocked.Increment(ref finishedCount);

                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            // Status progresivo
                            Status = $"{L["VideoStatus.Completed"]} {finishedCount}/{total} {L["VideoStatus.Downloads"]}";
                            
                            lock (lockObj)
                            {
                                if (errorMessages.Count > 0)
                                    Status += $" ({errorMessages.Count} con problemas)";
                            }

                            this.RaisePropertyChanged(nameof(Progress));
                            this.RaisePropertyChanged(nameof(Status));
                        });
                    }
                }).ToList();

                await Task.WhenAll(downloadTasks);

                Progress = 100;
                RemainingTime = ""; // Limpiar ETA al finalizar

                // Status final
                string finalStatus = $"{L["VideoStatus.Completed"]} {finishedCount}/{total} {L["VideoStatus.Downloads"]}";
                
                lock (lockObj)
                {
                    if (errorMessages.Count > 0)
                        finalStatus += $" ({errorMessages.Count} con problemas)";
                }

                Status = finalStatus;

                this.RaisePropertyChanged(nameof(Progress));
                this.RaisePropertyChanged(nameof(RemainingTime));
                this.RaisePropertyChanged(nameof(Status));
            }
            catch (OperationCanceledException)
            {
                Status = L["VideoStatus.OperationCanceled"];
                Progress = 0;
                RemainingTime = "";
                this.RaisePropertyChanged(nameof(Status));
                this.RaisePropertyChanged(nameof(Progress));
                this.RaisePropertyChanged(nameof(RemainingTime));
                _ = SoundManager.Play("fail.wav");
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