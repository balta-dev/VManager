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
using VManager.Views;

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
            CompressCommand = ReactiveCommand.CreateFromTask(CompressVideos, outputScheduler: AvaloniaScheduler.Instance);
            _ = LoadCodecsAsync();
        }

        private async Task CompressVideos()
        {
            HideFileReadyButton();
            _cts = new CancellationTokenSource();

            if (!int.TryParse(PorcentajeCompresionUsuario, out int percentValue) || percentValue <= 0 || percentValue > 100)
            {
                Status = "Porcentaje inválido.";
                this.RaisePropertyChanged(nameof(Status));
                return;
            }

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
                this.RaisePropertyChanged(nameof(IsConverting));
                this.RaisePropertyChanged(nameof(IsOperationRunning));

                int totalFiles = VideoPaths.Count;
                int currentFileIndex = 0;

                foreach (var video in VideoPaths)
                {
                    currentFileIndex++;
                    Status = $"Procesando ({currentFileIndex}/{totalFiles}): {Path.GetFileName(video)}...";
                    this.RaisePropertyChanged(nameof(Status));

                    var progress = new Progress<double>(p =>
                    {
                        // progreso relativo al archivo actual
                        double globalProgress = ((currentFileIndex - 1) + p) / totalFiles;
                        Progress = (int)(globalProgress * 100);
                        this.RaisePropertyChanged(nameof(Progress));
                    });

                    string outputPath = Path.Combine(
                        Path.GetDirectoryName(video)!,
                        Path.GetFileNameWithoutExtension(video) + $"-COMP-{percentValue}{Path.GetExtension(video)}"
                    );

                    var result = await processor.CompressAsync(
                        video,
                        outputPath,
                        percentValue,
                        SelectedVideoCodec,
                        SelectedAudioCodec,
                        progress,
                        _cts.Token
                    );

                    if (!result.Success)
                    {
                        SoundManager.Play("fail.wav");
                        Status = $"Error procesando {Path.GetFileName(video)}: {result.Message}";
                        Progress = 0;
                        this.RaisePropertyChanged(nameof(Status));
                        this.RaisePropertyChanged(nameof(Progress));
                        break; // Opcional: salir si un archivo falla
                    }

                    SoundManager.Play("success.wav");
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
                SoundManager.Play("fail.wav");
                Status = "Compresión cancelada por el usuario.";
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
