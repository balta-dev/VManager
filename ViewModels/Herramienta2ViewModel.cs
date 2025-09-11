using Avalonia.Controls;
using Avalonia.ReactiveUI;
using FFMpegCore;
using FFMpegCore.Enums;
using ReactiveUI;
using System;
using System.IO;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;

namespace VManager.ViewModels
{
    public class Herramienta2ViewModel : ViewModelBase
    {
        
        private string _porcentajeCompresionUsuario = "75"; // valor por defecto, 75%
        public string PorcentajeCompresionUsuario
        {
            get => _porcentajeCompresionUsuario;
            set => this.RaiseAndSetIfChanged(ref _porcentajeCompresionUsuario, value);
        }
        public ReactiveCommand<Unit, Unit> CompressCommand { get; }
        
        public Herramienta2ViewModel()
        {
            CompressCommand = ReactiveCommand.CreateFromTask(CompressVideo, outputScheduler: AvaloniaScheduler.Instance);
        }
        
        private async Task CompressVideo()
        {
            if (!File.Exists(VideoPath))
            {
                Status = "Archivo no encontrado.";
                this.RaisePropertyChanged(nameof(Status));
                return;
            }

            if (!int.TryParse(PorcentajeCompresionUsuario, out int percentValue) || percentValue <= 0 || percentValue > 100)
            {
                Status = "Porcentaje inválido.";
                this.RaisePropertyChanged(nameof(Status));
                return;
            }

            Status = "Obteniendo información del video...";
            this.RaisePropertyChanged(nameof(Status));

            var mediaInfo = await FFProbe.AnalyseAsync(VideoPath);
            double duration = mediaInfo.Duration.TotalSeconds;
            if (duration <= 0)
            {
                Status = "Error al obtener duración.";
                this.RaisePropertyChanged(nameof(Status));
                return;
            }

            long sizeBytes = new FileInfo(VideoPath).Length;
            if (sizeBytes <= 0)
            {
                Status = "Error al obtener tamaño.";
                this.RaisePropertyChanged(nameof(Status));
                return;
            }

            long targetSize = sizeBytes * percentValue / 100;
            int targetBitrate = (int)((targetSize * 8) / duration / 1000); // kbps

            string outputFile = Path.Combine(Path.GetDirectoryName(VideoPath)!, Path.GetFileNameWithoutExtension(VideoPath) + $"-{percentValue}.mp4");

            Status = "Comprimiendo...";
            this.RaisePropertyChanged(nameof(Status));

            await FFMpegArguments
                .FromFileInput(VideoPath)
                .OutputToFile(outputFile, overwrite: true, options => options
                    .WithVideoCodec(VideoCodec.LibX264)
                    .WithVideoBitrate(targetBitrate)
                    .WithAudioCodec(AudioCodec.Aac)
                    .WithAudioBitrate(128))
                    .NotifyOnProgress(time =>
                    {
                        Progress = (int)(time.TotalSeconds / duration * 100);
                        this.RaisePropertyChanged(nameof(Progress));
                    })
                .ProcessAsynchronously();

            Status = $"Listo: {outputFile}";
            Progress = 100;
            this.RaisePropertyChanged(nameof(Status));
            this.RaisePropertyChanged(nameof(Progress));
            
        }
        
    }
    
}