using Avalonia.ReactiveUI;
using ReactiveUI;
using System;
using System.IO;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
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
                int successCount = 0;

                foreach (var video in VideoPaths)
                {
                    currentFileIndex++;
                    Status = $"Comprimiendo ({currentFileIndex}/{totalFiles}): {Path.GetFileName(video)}...";
                    this.RaisePropertyChanged(nameof(Status));

                    var progress = new Progress<double>(p =>
                    {
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
                        _ = SoundManager.Play("fail.wav");
                        Status = $"Error procesando {Path.GetFileName(video)}: {result.Message}";
                        this.RaisePropertyChanged(nameof(Status));
                        break;
                    }

                    successCount++;
                    _ = SoundManager.Play("success.wav");
                    SetLastCompressedFile(result.OutputPath);
                }

                // Mensaje final más informativo
                Progress = 100;
                Status = successCount == totalFiles
                    ? $"✓ {successCount} archivo{(successCount > 1 ? "s" : "")} comprimido{(successCount > 1 ? "s" : "")} exitosamente"
                    : $"Proceso interrumpido: {successCount}/{totalFiles} archivos completados";

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
