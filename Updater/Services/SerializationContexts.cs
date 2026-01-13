// VManager/Services/SerializationContexts.cs
using System.Text.Json.Serialization;
using Updater.Services.Models;

namespace Updater.Services
{
    [JsonSerializable(typeof(UpdateInfo))]
    [JsonSourceGenerationOptions(
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        GenerationMode = JsonSourceGenerationMode.Metadata)]
    public partial class UpdaterJsonContext : JsonSerializerContext
    {
    }
}