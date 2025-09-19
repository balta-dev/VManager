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
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using VManager.Services;

namespace VManager.ViewModels
{
    public class Herramienta2ViewModel : CodecViewModelBase
    {
        
        // Listas de codecs
        public ObservableCollection<string> AvailableVideoCodecs { get; } = new();
        public ObservableCollection<string> AvailableAudioCodecs { get; } = new();

        // Selected
        private string _selectedVideoCodec = null;
        public string SelectedVideoCodec
        {
            get => _selectedVideoCodec;
            set => this.RaiseAndSetIfChanged(ref _selectedVideoCodec, value);
        }

        private string _selectedAudioCodec = null;
        public string SelectedAudioCodec
        {
            get => _selectedAudioCodec;
            set => this.RaiseAndSetIfChanged(ref _selectedAudioCodec, value);
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
            //RefreshCodecsCommand = ReactiveCommand.CreateFromTask(ReloadCodecsAsync, outputScheduler: AvaloniaScheduler.Instance);
            CompressCommand = ReactiveCommand.CreateFromTask(CompressVideo, outputScheduler: AvaloniaScheduler.Instance);
            // _ = LoadCodecsAsync();
        }
        
        private async Task CompressVideo()
        {
            HideFileReadyButton();

            if (!int.TryParse(PorcentajeCompresionUsuario, out int percentValue) || percentValue <= 0 || percentValue > 100)
            {
                Status = "Porcentaje inválido.";
                this.RaisePropertyChanged(nameof(Status));
                return;
            }

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
            
            var result = await processor.CompressAsync(
                VideoPath,
                OutputPath,
                percentValue,
                SelectedVideoCodec,
                SelectedAudioCodec,
                progress
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
        
    }
    
}