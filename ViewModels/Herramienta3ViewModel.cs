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
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using VManager.Services;

namespace VManager.ViewModels
{
    public class Herramienta3ViewModel : CodecViewModelBase
    {
        public VideoFormat SelectedFormat { get; set; } 
        public ReactiveCommand<Unit, Unit> ConvertCommand { get; }
        public ReactiveCommand<Unit, Unit> RefreshCodecsCommand { get; } 
        
        public ReactiveCommand<Unit, Unit> CancelConversionCommand { get; }
        public Herramienta3ViewModel()
        {
            RefreshCodecsCommand = ReactiveCommand.CreateFromTask(ReloadCodecsAsync, outputScheduler: AvaloniaScheduler.Instance);
            ConvertCommand = ReactiveCommand.CreateFromTask(ConvertVideo, outputScheduler: AvaloniaScheduler.Instance);
            CancelConversionCommand = ReactiveCommand.Create(CancelConversion, outputScheduler: AvaloniaScheduler.Instance);
            SelectedFormat = SupportedFormats[0]; //mp4
            _ = LoadCodecsAsync();
        }
        
        private CancellationTokenSource _cts;
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
            catch (OperationCanceledException)
            {
                SoundManager.Play("fail.wav");
                Status = "Conversión cancelada por el usuario.";
                Progress = 0;
                this.RaisePropertyChanged(nameof(Status));
                this.RaisePropertyChanged(nameof(Progress));
                
            }
            finally
            {
                // Limpiar el CancellationTokenSource
                _cts?.Dispose();
                _cts = null;
            }
            
        }
        
        public void CancelConversion()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                Console.WriteLine("[DEBUG]: Cancelación solicitada por el usuario.");
            }
        }
        
    }
    
}