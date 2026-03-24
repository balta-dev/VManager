// VManager/Services/SerializationContexts.cs
using System.Text.Json.Serialization;
using VManager.Services.Core.Execution;
using VManager.Services.Models;

namespace VManager.Services
{
    [JsonSerializable(typeof(AppConfig))]
    [JsonSerializable(typeof(ResumeProgress))]
    [JsonSerializable(typeof(YtDlpProgress))]
    [JsonSerializable(typeof(VideoInfo))]
    [JsonSerializable(typeof(FormatInfo))]
    [JsonSerializable(typeof(CodecCache))]
    [JsonSerializable(typeof(HardwareCapabilities))]
    [JsonSerializable(typeof(LogConfig))]
    [JsonSerializable(typeof(UpdateInfo))]
    [JsonSerializable(typeof(PlaylistInfo))]
    [JsonSourceGenerationOptions(
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        GenerationMode = JsonSourceGenerationMode.Metadata)]
    public partial class VManagerJsonContext : JsonSerializerContext
    {
    }
}