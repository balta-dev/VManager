using System.Collections.Generic;
using System.Linq;

namespace VManager.Services.Models;

public class HardwareCapabilities
{
        
    public bool Windows { get; set; }
        
    public bool Linux { get; set; }
        
    public bool Mac { get; set; }
    public bool Nvidia { get; set; }
    public bool AMD { get; set; }
    public bool Intel { get; set; }
    public bool VAAPI { get; set; }
    public bool WindowsMediaFoundation { get; set; }
    public bool VideoToolbox { get; set; }

    public override string ToString()
    {
        var capabilities = new List<string>();
            
        if (Windows) capabilities.Add("Windows");
        if (Linux) capabilities.Add("Linux");
        if (Mac) capabilities.Add("Mac");
            
        if (Nvidia) capabilities.Add("NVIDIA");
        if (AMD) capabilities.Add("AMD");
        if (Intel) capabilities.Add("Intel");
        if (VAAPI) capabilities.Add("VAAPI");
        if (WindowsMediaFoundation) capabilities.Add("Windows Media Foundation");
        if (VideoToolbox) capabilities.Add("VideoToolbox");
            
        return capabilities.Any() ? string.Join(", ", capabilities) : "Software Only";
    }
}