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
        
        public VideoFormat SelectedFormat { get; set; } 
        public ReactiveCommand<Unit, Unit> ConvertCommand { get; }
        public ReactiveCommand<Unit, Unit> RefreshCodecsCommand { get; } 
       
        public Herramienta3ViewModel()
        {
            RefreshCodecsCommand = ReactiveCommand.CreateFromTask(ReloadCodecsAsync, outputScheduler: AvaloniaScheduler.Instance);
            ConvertCommand = ReactiveCommand.CreateFromTask(ConvertVideo, outputScheduler: AvaloniaScheduler.Instance);
            SelectedFormat = SupportedFormats[0]; //mp4
            _ = LoadCodecsAsync();
        }
        private async Task ConvertVideo()
        {
            HideFileReadyButton();
            
            _cts = new CancellationTokenSource();

            try
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