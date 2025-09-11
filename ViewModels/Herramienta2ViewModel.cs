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
    public class Herramienta2ViewModel : ViewModelBase
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
        
        public Herramienta2ViewModel()
        {
            CompressCommand = ReactiveCommand.CreateFromTask(CompressVideo, outputScheduler: AvaloniaScheduler.Instance);
            TestCodecs();
            LoadCodecsAsync();
        }
        
        private async Task LoadCodecsAsync()
        {
            var codecService = new CodecService();
            var videoCodecs = await codecService.GetAvailableVideoCodecsAsync();
            var audioCodecs = await codecService.GetAvailableAudioCodecsAsync();

            AvailableVideoCodecs.Clear();
            foreach (var v in videoCodecs)
                AvailableVideoCodecs.Add(v);

            AvailableAudioCodecs.Clear();
            foreach (var a in audioCodecs)
                AvailableAudioCodecs.Add(a);
            
            SelectedVideoCodec = AvailableVideoCodecs.FirstOrDefault() ?? "libx264";
            SelectedAudioCodec = AvailableAudioCodecs.Contains("aac") 
                ? "aac" 
                : AvailableAudioCodecs.FirstOrDefault();
        }
        
        private async void TestCodecs() //debugging. muestra los codecs en consola.
        {
            var codecService = new VManager.Services.CodecService();
            var videoCodecs = await codecService.GetAvailableVideoCodecsAsync();
            var audioCodecs = await codecService.GetAvailableAudioCodecsAsync();

            Console.WriteLine("Video codecs:");
            foreach (var v in videoCodecs) Console.WriteLine(v);

            Console.WriteLine("Audio codecs:");
            foreach (var a in audioCodecs) Console.WriteLine(a);
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
            
            var videoCodec = SelectedVideoCodec ?? "libx264";
            var audioCodec = SelectedAudioCodec ?? "aac";

            // Preparar archivo de salida
            string outputFile = Path.Combine(
                Path.GetDirectoryName(VideoPath)!,
                Path.GetFileNameWithoutExtension(VideoPath) + $"-{percentValue}.mp4"
            );

            // Crear processor y progreso
            var processor = new VideoProcessor();
            var progress = new Progress<double>(p =>
            {
                Progress = (int)(p * 100);
                this.RaisePropertyChanged(nameof(Progress));
            });

            Status = "Comprimiendo...";
            this.RaisePropertyChanged(nameof(Status));

            // Ejecutar compresión
            var result = await processor.CompressAsync(
                VideoPath,
                outputFile,
                percentValue,
                videoCodec,
                audioCodec,
                progress
            );

            Status = result.Message;
            Progress = 100;
            this.RaisePropertyChanged(nameof(Status));
            this.RaisePropertyChanged(nameof(Progress));
        }
        
    }
    
}