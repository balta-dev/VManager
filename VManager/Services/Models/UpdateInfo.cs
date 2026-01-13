using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace VManager.Services.Models;

public class UpdateInfo
{
    [JsonConverter(typeof(VersionJsonConverter))]
    public required Version CurrentVersion { get; set; }
    
    [JsonConverter(typeof(VersionJsonConverter))]
    public required Version LatestVersion { get; set; }
    public required string DownloadUrl { get; set; }
    public required string ReleaseNotes { get; set; }
    public DateTime LastChecked { get; set; }
    public bool UpdateAvailable => LatestVersion > CurrentVersion;
}

public class VersionJsonConverter : JsonConverter<Version>
{
    public override Version Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return Version.Parse(reader.GetString() ?? "0.0.0");
    }

    public override void Write(Utf8JsonWriter writer, Version value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
