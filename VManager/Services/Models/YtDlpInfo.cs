using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VManager.Services.Models;

// ============================================================
//                    CLASES DE DATOS
// ============================================================

public class VideoInfo
{
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;
    
    [JsonPropertyName("duration")]
    public double Duration { get; set; }
    
    [JsonPropertyName("thumbnail")]
    public string Thumbnail { get; set; } = string.Empty;
    
    [JsonPropertyName("filesize")]
    public long? FileSize { get; set; }
    
    [JsonPropertyName("formats")]
    public List<FormatInfo> Formats { get; set; } = new();
    
    [JsonPropertyName("original_language")]
    public string? OriginalLanguage { get; set; }
}

public class FormatInfo
{
    [JsonPropertyName("format_id")]
    public string FormatId { get; set; } = string.Empty;
    
    [JsonPropertyName("ext")]
    public string Extension { get; set; } = string.Empty;
    
    [JsonPropertyName("resolution")]
    public string? Resolution { get; set; }
    
    [JsonPropertyName("height")]
    public int? Height { get; set; }
    
    [JsonPropertyName("width")]
    public int? Width { get; set; }
    
    [JsonPropertyName("filesize")]
    public long? FileSize { get; set; }
    
    [JsonPropertyName("vcodec")]
    public string VideoCodec { get; set; } = string.Empty;
    
    [JsonPropertyName("acodec")]
    public string AudioCodec { get; set; } = string.Empty;
    
    // AGREGAR ESTAS PROPIEDADES PARA AUDIO
    [JsonPropertyName("abr")]
    public double? Abr { get; set; } // Audio bitrate
    
    [JsonPropertyName("format_note")]
    public string? FormatNote { get; set; } // Descripción del formato
    
    [JsonPropertyName("asr")]
    public int? Asr { get; set; } // Audio sample rate
}