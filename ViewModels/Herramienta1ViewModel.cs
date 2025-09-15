using System;
using System.IO;
using System.Reactive;
using System.Threading.Tasks;
using Avalonia.ReactiveUI;
using ReactiveUI;
using VManager.Services;

namespace VManager.ViewModels
{
    
    public class Herramienta1ViewModel : ViewModelBase
    {

        private string _tiempoDesde = ""; 
        private string _tiempoHasta = ""; 
        public string TiempoDesde
        {
            get => _tiempoDesde;
            set => this.RaiseAndSetIfChanged(ref _tiempoDesde, value);
        }
        public string TiempoHasta
        {
            get => _tiempoHasta;
            set => this.RaiseAndSetIfChanged(ref _tiempoHasta, value);
        }
        public ReactiveCommand<Unit, Unit> CutCommand { get; }

        public Herramienta1ViewModel()
        {
            CutCommand = ReactiveCommand.CreateFromTask(CutVideo, outputScheduler: AvaloniaScheduler.Instance);
        }
        
        private bool TryParseTime(string input, out TimeSpan result)
        {
            result = TimeSpan.Zero;
            if (string.IsNullOrWhiteSpace(input))
                return false;

            // Intentar parsear formatos: "10" → segundos, "3:45" → minutos:segundos
            string[] parts = input.Split(':');

            try
            {
                if (parts.Length == 1)
                {
                    // Solo segundos
                    if (double.TryParse(parts[0], out double seconds))
                    {
                        result = TimeSpan.FromSeconds(seconds);
                        return true;
                    }
                }
                else if (parts.Length == 2)
                {
                    // Minutos:Segundos
                    if (int.TryParse(parts[0], out int minutes) &&
                        double.TryParse(parts[1], out double seconds))
                    {
                        result = new TimeSpan(0, 0, minutes, 0) + TimeSpan.FromSeconds(seconds);
                        return true;
                    }
                }
                else if (parts.Length == 3)
                {
                    // Horas:Minutos:Segundos
                    if (int.TryParse(parts[0], out int hours) &&
                        int.TryParse(parts[1], out int minutes) &&
                        double.TryParse(parts[2], out double seconds))
                    {
                        result = new TimeSpan(hours, minutes, (int)seconds);
                        return true;
                    }
                }
            }
            catch
            {
                return false;
            }

            return false;
        }
        
        private async Task CutVideo()
        {
            HideFileReadyButton();
            
            if (!TryParseTime(TiempoDesde, out TimeSpan start))
            {
                SoundManager.Play("fail.wav");
                Status = "Tiempo 'desde' inválido";
                this.RaisePropertyChanged(nameof(Status));
                return;
            }

            if (!TryParseTime(TiempoHasta, out TimeSpan end))
            {
                SoundManager.Play("fail.wav");
                Status = "Tiempo 'hasta' inválido";
                this.RaisePropertyChanged(nameof(Status));
                return;
            }

            if (end <= start)
            {
                SoundManager.Play("fail.wav");
                Status = "Tiempo 'hasta' debe ser mayor que tiempo 'desde'";
                this.RaisePropertyChanged(nameof(Status));
                return;
            }
            
            TimeSpan duration = end - start;

            Status = "Obteniendo información del video...";
            this.RaisePropertyChanged(nameof(Status));

            var processor = new VideoProcessor();
            var progress = new Progress<double>(p =>
            {
                Progress = (int)(p * 100);
                this.RaisePropertyChanged(nameof(Progress));
            });

            Status = "Cortando...";
            this.RaisePropertyChanged(nameof(Status));

            var result = await processor.CutAsync(
                VideoPath,
                OutputPath,
                start,
                duration,
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
        
        public string Mensaje => "Acá voy a meter una User Interface para VCUT";
    }
}