using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media.Imaging;

namespace VManager.Models
{
    public class VideoDownloadItem : INotifyPropertyChanged
    {
        private string _title = "Cargando...";
        private string _duration = string.Empty;
        private string _thumbnailUrl = string.Empty;
        private long? _fileSize;
        private ObservableCollection<VideoFormat> _availableFormats = new();
        private VideoFormat? _selectedFormat;
        private string _errorMessage = string.Empty;

        private double _progress;
        private string _status = string.Empty;
        private bool _isDownloading;
        private bool _isCompleted;
        private bool _isCanceled;
        private bool _isLoading = true;
        private bool _hasError;
        private Bitmap? _thumbnailBitmap;

        public string Url { get; set; } = string.Empty;


        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(); }
        }

        public string Duration
        {
            get => _duration;
            set { _duration = value; OnPropertyChanged(); }
        }

        public string ThumbnailUrl
        {
            get => _thumbnailUrl;
            set { _thumbnailUrl = value; OnPropertyChanged(); }
        }


        public Bitmap? ThumbnailBitmap
        {
            get => _thumbnailBitmap;
            set { _thumbnailBitmap = value; OnPropertyChanged(); }
        }


        public long? FileSize
        {
            get => _fileSize;
            set
            {
                _fileSize = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(FileSizeFormatted));
            }
        }


        public ObservableCollection<VideoFormat> AvailableFormats
        {
            get => _availableFormats;
            set { _availableFormats = value; OnPropertyChanged(); }
        }


        public VideoFormat? SelectedFormat
        {
            get => _selectedFormat;
            set { _selectedFormat = value; OnPropertyChanged(); }
        }


        public bool IsLoading
        {
            get => _isLoading;
            set { _isLoading = value; OnPropertyChanged(); }
        }

        public bool HasError
        {
            get => _hasError;
            set { _hasError = value; OnPropertyChanged(); }
        }


        public string ErrorMessage
        {
            get => _errorMessage;
            set { _errorMessage = value; OnPropertyChanged(); }
        }


        public double Progress
        {
            get => _progress;
            set { _progress = value; OnPropertyChanged(); }
        }


        public string Status
        {
            get => _status;
            set { _status = value; OnPropertyChanged(); }
        }


        public bool IsDownloading
        {
            get => _isDownloading;
            set { _isDownloading = value; OnPropertyChanged(); }
        }

        public bool IsCanceled
        {
            get => _isCanceled;
            set { _isCanceled = value; OnPropertyChanged(); }
        }
        
        public bool IsCompleted
        {
            get => _isCompleted;
            set { _isCompleted = value; OnPropertyChanged(); }
        }


        public string FileSizeFormatted =>
            FileSize.HasValue ? FormatFileSize(FileSize.Value) : "";


        private static string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;

            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len /= 1024;
            }

            return $"{len:0.##} {sizes[order]}";
        }


        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }



    public class VideoFormat
    {
        public string FormatId { get; set; } = string.Empty;
        public string Resolution { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public long? FileSize { get; set; }

        public string DisplayName => $"{Resolution} ({Extension})";
    }
}
