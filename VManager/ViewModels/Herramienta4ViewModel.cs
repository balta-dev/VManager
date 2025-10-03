using System;
using System.IO;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.ReactiveUI;
using DynamicData;
using ReactiveUI;
using VManager.Services;
using VManager.Views;

namespace VManager.ViewModels
{
    
    public class Herramienta4ViewModel : CodecViewModelBase
    {
        
        public AudioFormat SelectedAudioFormat { get; set; } 
        
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
        public ReactiveCommand<Unit, Unit> AudiofyCommand { get; }
        public Herramienta4ViewModel()
        {
            AudiofyCommand = ReactiveCommand.CreateFromTask(AudiofyVideo, outputScheduler: AvaloniaScheduler.Instance);
            SelectedAudioFormat = SupportedAudioFormats[0]; //mp3
            // Hardcodear códecs de audio estándar
            AvailableAudioCodecs.Clear();
            AvailableAudioCodecs.AddRange(new[]
            {
                "aac",
                "libmp3lame", 
                "libvorbis",
                "libopus",
                "flac"
            });
        }
        
        private async Task AudiofyVideo()
        {
            HideFileReadyButton();
            _cts = new CancellationTokenSource();

            try
            {
                Status = "Obteniendo información del video...";
                this.RaisePropertyChanged(nameof(Status));

                var processor = new VideoProcessor();
                var progress = new Progress<double>(p =>
                {
                    Progress = (int)(p * 100);
                    this.RaisePropertyChanged(nameof(Progress));
                });

                Status = "Extrayendo...";
                this.RaisePropertyChanged(nameof(Status));
                
                IsConverting = true;
                IsOperationRunning = true;
                this.RaisePropertyChanged(nameof(IsConverting));
                this.RaisePropertyChanged(nameof(IsOperationRunning));
        
                var result = await processor.AudiofyAsync(
                    VideoPath,
                    OutputPath,
                    SelectedVideoCodec,
                    SelectedAudioCodec,
                    SelectedAudioFormat?.Extension,
                    progress,
                    _cts.Token // Pasar el CancellationToken
                );
        
                if (result.Success)
                {
                    Notifier _notifier = new Notifier();
                    _notifier.ShowFileConvertedNotification(result.Message, result.OutputPath);
                    
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
                    IsConverting = false;
                    IsOperationRunning = false;
                    IsVideoPathSet = false; //para bloquear 
                    this.RaisePropertyChanged(nameof(IsOperationRunning));
                    this.RaisePropertyChanged(nameof(IsConverting));
                    this.RaisePropertyChanged(nameof(IsVideoPathSet));
                }
                else
                {
                    SoundManager.Play("fail.wav");
                    Status = result.Message; 
                    Progress = 0;
                    this.RaisePropertyChanged(nameof(Status));
                    this.RaisePropertyChanged(nameof(Progress));
                    IsConverting = false;
                    IsOperationRunning = false;
                    this.RaisePropertyChanged(nameof(IsConverting));
                    this.RaisePropertyChanged(nameof(IsOperationRunning));
                }
            }
            catch (OperationCanceledException)
            {
                SoundManager.Play("fail.wav");
                Status = "Extracción cancelada por el usuario.";
                Progress = 0;
                this.RaisePropertyChanged(nameof(Status));
                this.RaisePropertyChanged(nameof(Progress));
                IsConverting = false;
                IsOperationRunning = false;
                this.RaisePropertyChanged(nameof(IsConverting));
                this.RaisePropertyChanged(nameof(IsOperationRunning));
                
            }
            finally
            {
                // Limpiar el CancellationTokenSource
                _cts?.Dispose();
                _cts = null;
            }
            
        }
        
    }
}