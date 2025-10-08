using System;
using System.Collections.Generic;
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
            if (VideoPaths == null || VideoPaths.Count == 0)
                return;

            HideFileReadyButton();
            _cts = new CancellationTokenSource();

            var processor = new VideoProcessor();
            int totalFiles = VideoPaths.Count;
            int currentFileIndex = 0;

            try
            {
                IsConverting = true;
                IsOperationRunning = true;
                Progress = 0;

                foreach (var videoPath in VideoPaths)
                {
                    currentFileIndex++;
                    Status = $"Procesando archivo {currentFileIndex} de {totalFiles}...";
                    this.RaisePropertyChanged(nameof(Status));

                    var progress = new Progress<double>(p =>
                    {
                        // Progreso combinado
                        Progress = (int)(((currentFileIndex - 1) + p) / totalFiles * 100);
                        this.RaisePropertyChanged(nameof(Progress));
                    });

                    string outputPath = GetOutputPath(videoPath);

                    var extension = Path.GetExtension(videoPath).ToLowerInvariant();
                    bool isAudioInput = extension is ".mp3" or ".wav" or ".flac" or ".ogg" or ".m4a";

                    ProcessingResult result;
                    if (isAudioInput)
                    {
                        Status = $"Convirtiendo audio ({Path.GetFileName(videoPath)})...";
                        result = await processor.AudiofyAsync(
                            videoPath,
                            outputPath,
                            null,
                            SelectedAudioCodec,
                            SelectedAudioFormat?.Extension,
                            progress,
                            _cts.Token
                        );
                    }
                    else
                    {
                        Status = $"Extrayendo audio ({Path.GetFileName(videoPath)})...";
                        result = await processor.AudiofyAsync(
                            videoPath,
                            outputPath,
                            SelectedVideoCodec,
                            SelectedAudioCodec,
                            SelectedAudioFormat?.Extension,
                            progress,
                            _cts.Token
                        );
                    }

                    if (result.Success)
                    {
                        var notifier = new Notifier();
                        notifier.ShowFileConvertedNotification(result.Message, result.OutputPath);
                        SoundManager.Play("success.wav");
                        SetLastCompressedFile(result.OutputPath);
                        Warning = result.Warning;
                    }
                    else
                    {
                        SoundManager.Play("fail.wav");
                        Status = result.Message;
                    }
                }

                if (VideoPaths.Count == 1)
                {
                    Status = $"Archivo procesado: {Path.GetFileName(VideoPaths[0])}";
                }
                else if (VideoPaths.Count > 1)
                {
                    Status = $"Todos los archivos procesados. Último: {Path.GetFileName(VideoPaths[^1])}";
                }
                Progress = 100;
            }
            catch (OperationCanceledException)
            {
                SoundManager.Play("fail.wav");
                Status = "Operación cancelada.";
                Progress = 0;
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
