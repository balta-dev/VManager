using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace VManager.Services.Models;

/// <summary>
/// Representa la respuesta de yt-dlp con --flat-playlist -J.
/// El campo _type puede ser "playlist" o "video".
/// </summary>
public class PlaylistInfo
{
    [JsonPropertyName("_type")]
    public string? Type { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    /// <summary>
    /// Thumbnail de la playlist (primer video generalmente).
    /// </summary>
    [JsonPropertyName("thumbnail")]
    public string? Thumbnail { get; set; }

    /// <summary>
    /// Lista de thumbnails alternativos.
    /// </summary>
    [JsonPropertyName("thumbnails")]
    public List<PlaylistThumbnail>? Thumbnails { get; set; }

    /// <summary>
    /// Entradas de la playlist (cada una es un video con info básica).
    /// </summary>
    [JsonPropertyName("entries")]
    public List<PlaylistEntry>? Entries { get; set; }

    /// <summary>
    /// Cantidad total de videos. Puede estar presente aunque entries esté truncado.
    /// </summary>
    [JsonPropertyName("playlist_count")]
    public int? PlaylistCount { get; set; }

    /// <summary>
    /// Devuelve true si este JSON representa una playlist real (no un video suelto).
    /// </summary>
    [JsonIgnore]
    public bool IsPlaylist => Type == "playlist";

    /// <summary>
    /// Mejor URL de thumbnail disponible.
    /// </summary>
    [JsonIgnore]
    public string? BestThumbnailUrl =>
        !string.IsNullOrEmpty(Thumbnail)
            ? Thumbnail
            : Thumbnails?.LastOrDefault()?.Url; // yt-dlp ordena de menor a mayor calidad
}

public class PlaylistEntry
{
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("duration")]
    public double? Duration { get; set; }

    [JsonPropertyName("thumbnail")]
    public string? Thumbnail { get; set; }

    [JsonPropertyName("thumbnails")]
    public List<PlaylistThumbnail>? Thumbnails { get; set; }

    /// <summary>
    /// Mejor URL de thumbnail disponible para esta entrada.
    /// </summary>
    [JsonIgnore]
    public string? BestThumbnailUrl =>
        !string.IsNullOrEmpty(Thumbnail)
            ? Thumbnail
            : Thumbnails?.LastOrDefault()?.Url;

    /// <summary>
    /// URL navegable del video. Algunos extractores la devuelven directamente,
    /// otros solo devuelven el ID. Se construye a partir de lo disponible.
    /// </summary>
    [JsonIgnore]
    public string? WebpageUrl { get; set; } // seteado manualmente por el ViewModel si hace falta

    [JsonPropertyName("webpage_url")]
    public string? WebpageUrlRaw { get; set; }

    /// <summary>
    /// URL definitiva para descargar este entry: webpage_url si está, sino url.
    /// </summary>
    [JsonIgnore]
    public string? EffectiveUrl => !string.IsNullOrEmpty(WebpageUrlRaw) ? WebpageUrlRaw : Url;
}

public class PlaylistThumbnail
{
    [JsonPropertyName("url")]
    public string? Url { get; set; }
}
