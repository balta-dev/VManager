using System;
using System.Collections.Generic;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.ReactiveUI;
using ReactiveUI;
using VManager.Services;

namespace VManager.ViewModels
{
    
    public class Herramienta1ViewModel : ViewModelBase
    {

        private string _tiempoDesde = ""; 
        private string _tiempoHasta = ""; 
        public string TiempoDesde
        {
            get => _tiempoDesde;
            set => this.RaiseAndSetIfChanged(ref _tiempoDesde, value);
        }
        public string TiempoHasta
        {
            get => _tiempoHasta;
            set => this.RaiseAndSetIfChanged(ref _tiempoHasta, value);
        }
        
        private bool _isConverting;
        public bool IsConverting
        {
            get => _isConverting;
            set => this.RaiseAndSetIfChanged(ref _isConverting, value);
        }
        
        private bool isVideoPathSet;
        public override bool IsVideoPathSet
        {
            get => isVideoPathSet;
            set => this.RaiseAndSetIfChanged(ref isVideoPathSet, value);
        }
        public ReactiveCommand<Unit, Unit> CutCommand { get; }
        public ReactiveCommand<Unit, Unit> BrowseSingleVideoCommand { get; }
        public Herramienta1ViewModel()
        {
            CutCommand = ReactiveCommand.CreateFromTask(CutVideo, outputScheduler: AvaloniaScheduler.Instance);
            BrowseSingleVideoCommand = ReactiveCommand.CreateFromTask(BrowseSingleVideo, outputScheduler: AvaloniaScheduler.Instance);
        }
        
        private async Task BrowseSingleVideo()
        {
            TopLevel? topLevel = null;
            if (App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop &&
                desktop.MainWindow != null)
            {
                topLevel = TopLevel.GetTopLevel(desktop.MainWindow);
            }

            if (topLevel == null)
            {
                Status = "No se pudo acceder a la ventana principal.";
                this.RaisePropertyChanged(nameof(Status));
                return;
            }

            var videoPatterns = new[] { "*.mp4", "*.mkv", "*.avi", "*.mov", "*.webm", "*.wmv", "*.flv", "*.3gp"};

            var filters = new List<FilePickerFileType>
            {
                new FilePickerFileType("Videos") { Patterns = videoPatterns }
            };

            var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Seleccionar video",
                FileTypeFilter = filters,
                AllowMultiple = false //este método existe solo por esto
            });

            if (files.Count > 0)
            {
                VideoPath = files[0].Path.LocalPath;
                this.RaisePropertyChanged(nameof(VideoPath));
                IsVideoPathSet = true;
                this.RaisePropertyChanged(nameof(IsVideoPathSet));
            }
        }
        
        private bool TryParseTime(string input, out TimeSpan result)
        {
            result = TimeSpan.Zero;
            if (string.IsNullOrWhiteSpace(input))
                return false;

            // Intentar parsear formatos: "10" → segundos, "3:45" → minutos:segundos
            string[] parts = input.Split(':');

            try
            {
                if (parts.Length == 1)
                {
                    // Solo segundos
                    if (double.TryParse(parts[0], out double seconds))
                    {
                        result = TimeSpan.FromSeconds(seconds);
                        return true;
                    }
                }
                else if (parts.Length == 2)
                {
                    // Minutos:Segundos
                    if (int.TryParse(parts[0], out int minutes) &&
                        double.TryParse(parts[1], out double seconds))
                    {
                        result = new TimeSpan(0, 0, minutes, 0) + TimeSpan.FromSeconds(seconds);
                        return true;
                    }
                }
                else if (parts.Length == 3)
                {
                    // Horas:Minutos:Segundos
                    if (int.TryParse(parts[0], out int hours) &&
                        int.TryParse(parts[1], out int minutes) &&
                        double.TryParse(parts[2], out double seconds))
                    {
                        result = new TimeSpan(hours, minutes, (int)seconds);
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }
        
        public override void ClearInfo()
        {
            // Primero ejecuta el ClearInfo del base
            base.ClearInfo();

            // Ahora resetea propiedades propias de Herramienta1
            TiempoDesde = "";
            TiempoHasta = "";
            IsConverting = false;

            this.RaisePropertyChanged(nameof(TiempoDesde));
            this.RaisePropertyChanged(nameof(TiempoHasta));
            this.RaisePropertyChanged(nameof(IsConverting));
        }
        
        private async Task CutVideo()
        {
            HideFileReadyButton();
            _cts = new CancellationTokenSource();

            if (!TryParseTime(TiempoDesde, out TimeSpan start))
            {
                _ = SoundManager.Play("fail.wav");
                Status = "Tiempo 'desde' inválido";
                this.RaisePropertyChanged(nameof(Status));
                TiempoDesde = "";
                this.RaisePropertyChanged(nameof(TiempoDesde));
                return;
            }

            // Obtener información del video primero para conocer su duración
            Status = "Obteniendo información del video...";
            this.RaisePropertyChanged(nameof(Status));
            
            var processor = new VideoProcessor();
            var analysisResult = await processor.AnalyzeVideoAsync(VideoPath);
            
            if (!analysisResult.Success)
            {
                _ = SoundManager.Play("fail.wav");
                Status = analysisResult.Message;
                this.RaisePropertyChanged(nameof(Status));
                return;
            }
            
            var mediaInfo = analysisResult.Result!;
            double totalDuration = mediaInfo.Duration.TotalSeconds;

            TimeSpan duration;
            
            // Si TiempoHasta está vacío, usar hasta el final del video
            if (string.IsNullOrWhiteSpace(TiempoHasta))
            {
                duration = TimeSpan.FromSeconds(totalDuration - start.TotalSeconds);
                duration += TimeSpan.FromSeconds(1); // PARA TRIGGEREAR LA ADVERTENCIA DE REDIMENSIÓN AL CUTASYNC
                
                if (duration <= TimeSpan.Zero)
                {
                    _ = SoundManager.Play("fail.wav");
                    Status = "El tiempo 'desde' excede la duración del video";
                    this.RaisePropertyChanged(nameof(Status));
                    return;
                }
            }
            else
            {
                // Si TiempoHasta tiene valor, parsearlo y validar
                if (!TryParseTime(TiempoHasta, out TimeSpan end))
                {
                    _ = SoundManager.Play("fail.wav");
                    Status = "Tiempo 'hasta' inválido";
                    this.RaisePropertyChanged(nameof(Status));
                    TiempoHasta = "";
                    this.RaisePropertyChanged(nameof(TiempoHasta));
                    return;
                }

                if (end <= start)
                {
                    _ = SoundManager.Play("fail.wav");
                    Status = "Tiempo 'hasta' debe ser mayor que tiempo 'desde'";
                    this.RaisePropertyChanged(nameof(Status));
                    return;
                }

                duration = end - start;
            }

            try
            {
                var progress = new Progress<IVideoProcessor.ProgressInfo>(info =>
                {
                    Progress = (int)(info.Progress * 100);
                    RemainingTime = info.Remaining.ToString(@"mm\:ss");
                    this.RaisePropertyChanged(nameof(Progress));
                    this.RaisePropertyChanged(nameof(RemainingTime));
                });

                Status = "Cortando...";
                IsConverting = true;
                IsOperationRunning = true;
                this.RaisePropertyChanged(nameof(Status));
                this.RaisePropertyChanged(nameof(IsConverting));
                this.RaisePropertyChanged(nameof(IsOperationRunning));

                var result = await processor.CutAsync(
                    VideoPath,
                    OutputPath,
                    start,
                    duration,
                    progress,
                    _cts.Token 
                );

                if (result.Success)
                {
                    Notifier _notifier = new Notifier();
                    _notifier.ShowFileConvertedNotification(result.Message, result.OutputPath);
                    
                    _ = SoundManager.Play("success.wav");
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
                    _ = SoundManager.Play("fail.wav");
                    Status = result.Message;
                    Progress = 0;
                    this.RaisePropertyChanged(nameof(Status));
                    this.RaisePropertyChanged(nameof(Progress));
                }
            }
            catch (OperationCanceledException)
            {
                await SoundManager.Play("fail.wav");
                Status = "Corte cancelado por el usuario.";
                Progress = 0;
                this.RaisePropertyChanged(nameof(Status));
                this.RaisePropertyChanged(nameof(Progress));
            }
            finally
            {
                IsConverting = false;
                IsOperationRunning = false;
                this.RaisePropertyChanged(nameof(IsConverting));
                this.RaisePropertyChanged(nameof(IsOperationRunning));

                _cts?.Dispose();
                _cts = null;
            }
        }
        
    }
}