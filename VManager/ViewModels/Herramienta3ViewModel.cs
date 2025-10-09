using Avalonia.ReactiveUI;
using ReactiveUI;
using System;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
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
        
        private VideoFormat _selectedFormat = new VideoFormat();
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
                SelectedAudioCodec = AvailableAudioCodecs.FirstOrDefault()!;
        }

        
        private async Task ConvertVideo()
        {
            HideFileReadyButton();
            _cts = new CancellationTokenSource();

            try
            {
                var processor = new VideoProcessor();

                var progress = new Progress<double>(p =>
                {
                    Progress = (int)(p * 100);
                    this.RaisePropertyChanged(nameof(Progress));
                });

                IsConverting = true;
                IsOperationRunning = true;
                this.RaisePropertyChanged(nameof(IsConverting));
                this.RaisePropertyChanged(nameof(IsOperationRunning));

                foreach (var video in VideoPaths)
                {
                    Status = $"Procesando: {Path.GetFileName(video)}...";
                    this.RaisePropertyChanged(nameof(Status));

                    string outputPath = Path.Combine(
                        Path.GetDirectoryName(video)!,
                        Path.GetFileNameWithoutExtension(video) + $"-VCONV.{SelectedFormat?.Extension}"
                    );

                    var result = await processor.ConvertAsync(
                        video,
                        outputPath,
                        SelectedVideoCodec,
                        SelectedAudioCodec,
                        SelectedFormat?.Extension!,
                        progress,
                        _cts.Token
                    );

                    if (!result.Success)
                    {
                        _ = SoundManager.Play("fail.wav");
                        Status = $"Error procesando {Path.GetFileName(video)}: {result.Message}";
                        Progress = 0;
                        this.RaisePropertyChanged(nameof(Status));
                        break; // Opcional: salir si un archivo falla
                    }

                    _ = SoundManager.Play("success.wav");
                    SetLastCompressedFile(result.OutputPath);
                }

                // Mensaje final
                if (VideoPaths.Count == 1)
                    Status = $"Archivo procesado: {Path.GetFileName(VideoPaths[0])}";
                else
                    Status = $"Todos los archivos procesados. Último: {Path.GetFileName(VideoPaths[^1])}";

                Progress = 100;
                IsConverting = false;
                IsOperationRunning = false;
                IsVideoPathSet = false;
                this.RaisePropertyChanged(nameof(Status));
                this.RaisePropertyChanged(nameof(Progress));
                this.RaisePropertyChanged(nameof(IsConverting));
                this.RaisePropertyChanged(nameof(IsOperationRunning));
                this.RaisePropertyChanged(nameof(IsVideoPathSet));
            }
            catch (OperationCanceledException)
            {
                _ = SoundManager.Play("fail.wav");
                Status = "Conversión cancelada por el usuario.";
                Progress = 0;
                IsConverting = false;
                IsOperationRunning = false;
                this.RaisePropertyChanged(nameof(Status));
                this.RaisePropertyChanged(nameof(Progress));
                this.RaisePropertyChanged(nameof(IsConverting));
                this.RaisePropertyChanged(nameof(IsOperationRunning));
            }
            finally
            {
                _cts?.Dispose();
                _cts = null;
            }
        }

        
    }
    
}