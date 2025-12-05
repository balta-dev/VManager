using System;
using System.IO;
using System.Reactive;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.ReactiveUI;
using ReactiveUI;
using VManager.Services;

namespace VManager.ViewModels
{
public class Herramienta5ViewModel : CodecViewModelBase
{
public AudioFormat SelectedAudioFormat { get; set; }
    private bool _isConverting;
    public bool IsConverting
    {
        get => _isConverting;
        set => this.RaiseAndSetIfChanged(ref _isConverting, value);
    }

    public ReactiveCommand<Unit, Unit> DownloadCommand { get; }
    public ReactiveCommand<string, Unit> AddUrlCommand { get; }
    public ReactiveCommand<string, Unit> RemoveUrlCommand { get; }

    public Herramienta5ViewModel()
    {
        DownloadCommand = ReactiveCommand.CreateFromTask(DownloadVideos, outputScheduler: AvaloniaScheduler.Instance);
        AddUrlCommand = ReactiveCommand.Create<string>(AddUrl, outputScheduler: AvaloniaScheduler.Instance);
        RemoveUrlCommand = ReactiveCommand.Create<string>(RemoveUrl, outputScheduler: AvaloniaScheduler.Instance);
        SelectedAudioFormat = SupportedAudioFormats[0]; // mp3
    }

    // Ahora IsVideoPathSet significa "hay URL cargadas"
    public override bool IsVideoPathSet
    {
        get => VideoPaths.Count > 0;
        set { /* requerido por la clase base, no se usa */ }
    }
    

    // Método público para agregar URLs desde la UI
    public void AddUrl(string url)
    {
        if (!string.IsNullOrWhiteSpace(url))
        {
            VideoPaths.Add(url);
            this.RaisePropertyChanged(nameof(IsVideoPathSet));
        }
    }

    public void RemoveUrl(string url)
    {
        VideoPaths.Remove(url);
        this.RaisePropertyChanged(nameof(IsVideoPathSet));
    }

    protected override bool AllowAudioFiles => false;

    private async Task DownloadVideos()
    {
        if (VideoPaths.Count == 0)
        {
            Status = "No hay URLs para descargar.";
            this.RaisePropertyChanged(nameof(Status));
            return;
        }

        HideFileReadyButton();
        _cts = new CancellationTokenSource();

        try
        {
            var processor = new YtDlpProcessor();

            IsConverting = true;
            IsOperationRunning = true;
            Progress = 0;

            int total = VideoPaths.Count;
            int index = 0;
            int success = 0;

            foreach (var url in VideoPaths)
            {
                index++;

                var progress = new Progress<YtDlpProcessor.YtDlpProgress>(p =>
                {
                    if (p.Eta == "Preparando...")
                    {
                        Status = "Preparando descarga...";
                        this.RaisePropertyChanged(nameof(Status));
                        return;
                    }
                    
                    Status = $"Descargando enlace {index}/{total}";
                    this.RaisePropertyChanged(nameof(Status));
                    
                    double global = ((index - 1) + p.Progress) / total;
                    Progress = (int)(global * 100);

                    RemainingTime = p.Eta;
                    Console.WriteLine($"PROGRESS: {p.Progress}, SPEED:{p.Speed}, ETA:{p.Eta}");

                    this.RaisePropertyChanged(nameof(Progress));
                    this.RaisePropertyChanged(nameof(RemainingTime));
                });

                // Guardamos en el Escritorio
                string outputTemplate =
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                    $"%(title)s.{SelectedAudioFormat.Extension}");

                var result = await processor.DownloadAsync(
                    url,
                    outputTemplate,
                    progress,
                    _cts.Token
                );

                if (result.Success)
                {
                    success++;

                    var notifier = new Notifier();
                    notifier.ShowFileConvertedNotification("Descargado", result.OutputPath);

                    _ = SoundManager.Play("success.wav");
                    SetLastCompressedFile(result.OutputPath);
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

            Status = $"Completado {success}/{total} descargas";
            this.RaisePropertyChanged(nameof(Status));
        }
        catch (OperationCanceledException)
        {
            _ = SoundManager.Play("fail.wav");
            Status = "Operación cancelada.";
            Progress = 0;
            this.RaisePropertyChanged(nameof(Status));
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
}
}
