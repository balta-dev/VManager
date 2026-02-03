using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using VManager.Services;

namespace VManager.ViewModels
{
    public interface ICodecViewModel : INotifyPropertyChanged
    {
        string Status { get; }
        string Warning { get; }
        int Progress { get; }
        string OutputPath { get; }
        string VideoPath { get; }
        bool IsFileReadyVisible { get; }
        double GridWidth { set; }
        double HeightBlock { set; }
        
    }

    public class VideoFormat
    {
        public string Extension { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public override string ToString() => DisplayName;
    }
    
    public class AudioFormat 
    {
        public string Codec { get; set; } = string.Empty;
        public string Extension { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public override string ToString() => DisplayName;
    }
}