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
    public class Herramienta2ViewModel : CodecViewModelBase
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
        
        private string _porcentajeCompresionUsuario = "75"; // valor por defecto, 75%
        public string PorcentajeCompresionUsuario
        {
            get => _porcentajeCompresionUsuario;
            set => this.RaiseAndSetIfChanged(ref _porcentajeCompresionUsuario, value);
        }
        public ReactiveCommand<Unit, Unit> CompressCommand { get; }
        public ReactiveCommand<Unit, Unit> RefreshCodecsCommand { get; } 
        
        public Herramienta2ViewModel()
        {
            RefreshCodecsCommand = ReactiveCommand.CreateFromTask(ReloadCodecsAsync, outputScheduler: AvaloniaScheduler.Instance);
            CompressCommand = ReactiveCommand.CreateFromTask(CompressVideo, outputScheduler: AvaloniaScheduler.Instance);
            _ = LoadCodecsAsync();
        }
        
        private async Task CompressVideo()
        {
        HideFileReadyButton();

        _cts = new CancellationTokenSource();

        if (!int.TryParse(PorcentajeCompresionUsuario, out int percentValue) || percentValue <= 0 || percentValue > 100)
        {
            Status = "Porcentaje inválido.";
            this.RaisePropertyChanged(nameof(Status));
            return;
        }

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

            Status = "Comprimiendo...";
            this.RaisePropertyChanged(nameof(Status));

            IsConverting = true;
            IsOperationRunning = true;
            this.RaisePropertyChanged(nameof(IsConverting));
            this.RaisePropertyChanged(nameof(IsOperationRunning));

            var result = await processor.CompressAsync(
                VideoPath,
                OutputPath,
                percentValue,
                SelectedVideoCodec,
                SelectedAudioCodec,
                progress,
                _cts.Token // <-- Pasamos el CancellationToken
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
            Status = "Compresión cancelada por el usuario.";
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

            // Limpiar el CancellationTokenSource
            _cts?.Dispose();
            _cts = null;
        }
        }

        
    }
    
}