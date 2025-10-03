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
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using VManager.Services;
using VManager.Views;

namespace VManager.ViewModels
{
    public class Herramienta3ViewModel : CodecViewModelBase
    {
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
        
        private VideoFormat _selectedFormat;
        public VideoFormat SelectedFormat
        {
            get => _selectedFormat;
            set => this.RaiseAndSetIfChanged(ref _selectedFormat, value);
        }
        public ReactiveCommand<Unit, Unit> ConvertCommand { get; }
        public ReactiveCommand<Unit, Unit> RefreshCodecsCommand { get; } 
       
        public Herramienta3ViewModel()
        {
            RefreshCodecsCommand = ReactiveCommand.CreateFromTask(ReloadCodecsAsync, outputScheduler: AvaloniaScheduler.Instance);
            ConvertCommand = ReactiveCommand.CreateFromTask(ConvertVideo, outputScheduler: AvaloniaScheduler.Instance);
            SelectedFormat = SupportedVideoFormats[0]; //mp4
            _ = LoadCodecsAsync();
            this.WhenAnyValue(x => x.SelectedVideoCodec, x => x.SelectedFormat)
                .Subscribe(_ => UpdateAudioCodecsForSelectedVideo());

        }
        
        private void UpdateAudioCodecsForSelectedVideo()
        {
            var compatibleAudio = _allAudioCodecs.ToList();

            // Filtrado según SelectedVideoCodec
            if (SelectedVideoCodec == "libvpx" || SelectedVideoCodec == "webm")
            {
                compatibleAudio = compatibleAudio
                    .Where(a => a == "libvorbis" || a == "libopus")
                    .ToList();
            }

            // Filtrado según SelectedFormat
            if (SelectedFormat != null)
            {
                switch (SelectedFormat.Extension.ToLower())
                {
                    case "mp4":
                    case "m4v":
                        // MP4/M4V típicamente soporta AAC, MP3, FLAC
                        compatibleAudio = compatibleAudio
                            .Where(a => a == "aac" || a == "libmp3lame" || a == "flac")
                            .ToList();
                        break;

                    case "webm":
                    case "vp8":
                    case "vp9":
                        // WebM soporta Vorbis u Opus
                        compatibleAudio = compatibleAudio
                            .Where(a => a == "libvorbis" || a == "libopus")
                            .ToList();
                        break;

                    case "mkv":
                        // MKV es muy flexible: acepta casi todos
                        compatibleAudio = compatibleAudio
                            .Where(a => a == "aac" || a == "libmp3lame" || a == "flac" || a == "libopus" || a == "libvorbis")
                            .ToList();
                        break;

                    case "mov":
                        // MOV soporta AAC y FLAC
                        compatibleAudio = compatibleAudio
                            .Where(a => a == "aac" || a == "flac")
                            .ToList();
                        break;

                    default:
                        // Otros formatos no filtran nada
                        break;
                }
            }

            // Limpiar y actualizar ObservableCollection
            AvailableAudioCodecs.Clear();
            foreach (var a in compatibleAudio)
                AvailableAudioCodecs.Add(a);

            // Asegurarse de que SelectedAudioCodec sigue siendo válido
            if (!AvailableAudioCodecs.Contains(SelectedAudioCodec))
                SelectedAudioCodec = AvailableAudioCodecs.FirstOrDefault();
        }

        
        private async Task ConvertVideo()
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

                Status = "Convirtiendo...";
                this.RaisePropertyChanged(nameof(Status));
                
                IsConverting = true;
                IsOperationRunning = true;
                this.RaisePropertyChanged(nameof(IsConverting));
                this.RaisePropertyChanged(nameof(IsOperationRunning));
        
                var result = await processor.ConvertAsync(
                    VideoPath,
                    OutputPath,
                    SelectedVideoCodec,
                    SelectedAudioCodec,
                    SelectedFormat?.Extension,
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
                Status = "Conversión cancelada por el usuario.";
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