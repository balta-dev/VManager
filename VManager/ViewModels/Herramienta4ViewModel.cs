using System;
using System.IO;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.ReactiveUI;
using DynamicData;
using ReactiveUI;
using VManager.Services;

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
            AudiofyCommand = ReactiveCommand.CreateFromTask(AudiofyVideos, outputScheduler: AvaloniaScheduler.Instance);
            SelectedAudioFormat = SupportedAudioFormats[0]; // mp3

            // Códecs de audio estándar
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

        protected override bool AllowAudioFiles => true;

        private async Task AudiofyVideos()
        {
            if (VideoPaths.Count == 0) return;

            HideFileReadyButton();
            _cts = new CancellationTokenSource();
            
            if (VideoPaths.Count == 0)
            {
                Status = "No hay archivos seleccionados.";
                this.RaisePropertyChanged(nameof(Status));
                return;
            }

            try
            {
                var processor = new VideoProcessor();
                IsConverting = true;
                IsOperationRunning = true;
                Progress = 0;

                int totalFiles = VideoPaths.Count;
                int currentFileIndex = 0;
                int successCount = 0;

                foreach (var videoPath in VideoPaths)
                {
                    currentFileIndex++;
                    
                    var progress = new Progress<double>(p =>
                    {
                        double globalProgress = ((currentFileIndex - 1) + p) / totalFiles;
                        Progress = (int)(globalProgress * 100);
                        this.RaisePropertyChanged(nameof(Progress));
                    });

                    string outputPath = GetOutputPath(videoPath);
                    var extension = Path.GetExtension(videoPath).ToLowerInvariant();
                    bool isAudioInput = extension is ".mp3" or ".wav" or ".flac" or ".ogg" or ".m4a";

                    Status = isAudioInput
                        ? $"Convirtiendo audio ({currentFileIndex}/{totalFiles}): {Path.GetFileName(videoPath)}..."
                        : $"Extrayendo audio ({currentFileIndex}/{totalFiles}): {Path.GetFileName(videoPath)}...";
                    this.RaisePropertyChanged(nameof(Status));

                    var result = await processor.AudiofyAsync(
                        videoPath,
                        outputPath,
                        isAudioInput ? null : SelectedVideoCodec,
                        SelectedAudioCodec,
                        SelectedAudioFormat?.Extension!,
                        progress,
                        _cts.Token
                    );

                    if (result.Success)
                    {
                        successCount++;
                        var notifier = new Notifier();
                        notifier.ShowFileConvertedNotification(result.Message, result.OutputPath);
                        _ = SoundManager.Play("success.wav");
                        SetLastCompressedFile(result.OutputPath);
                        Warning = result.Warning;
                    }
                    else
                    {
                        _ = SoundManager.Play("fail.wav");
                        Status = result.Message;
                        this.RaisePropertyChanged(nameof(Status));
                        break;
                    }
                }

                Progress = 100;
                Status = successCount == totalFiles
                    ? $"✓ {successCount} archivo{(successCount > 1 ? "s" : "")} procesado{(successCount > 1 ? "s" : "")} exitosamente"
                    : $"Proceso interrumpido: {successCount}/{totalFiles} archivos completados";
                
                this.RaisePropertyChanged(nameof(Status));
                this.RaisePropertyChanged(nameof(Progress));
            }
            catch (OperationCanceledException)
            {
                _ = SoundManager.Play("fail.wav");
                Status = "Operación cancelada.";
                Progress = 0;
                this.RaisePropertyChanged(nameof(Status));
                this.RaisePropertyChanged(nameof(Progress));
            }
            finally
            {
                IsConverting = false;
                IsOperationRunning = false;
                _cts?.Dispose();
                _cts = null;
                
                this.RaisePropertyChanged(nameof(IsConverting));
                this.RaisePropertyChanged(nameof(IsOperationRunning));
            }
        }

        // Genera un OutputPath único por archivo
        private string GetOutputPath(string inputPath)
        {
            string directory = Path.GetDirectoryName(inputPath)!;
            string fileName = Path.GetFileNameWithoutExtension(inputPath);
            string extension = SelectedAudioFormat?.Extension ?? "mp3";
            return Path.Combine(directory, $"{fileName}-ACONV.{extension}");
        }
    }
}
