using System.Collections.Generic;

namespace VManager.Services.Models;

public class CodecCache
{
    public List<string> VideoCodecs { get; set; } = new();
    public List<string> AudioCodecs { get; set; } = new();
    public HardwareCapabilities Hardware { get; set; } = new();
}