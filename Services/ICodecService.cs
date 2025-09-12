using System.Collections.Generic;
using System.Threading.Tasks;

namespace VManager.Services
{
    public interface ICodecService
    {
        Task<IReadOnlyList<string>> GetAvailableVideoCodecsAsync();
        Task<IReadOnlyList<string>> GetAvailableAudioCodecsAsync();
        Task<HardwareCapabilities> GetHardwareCapabilitiesAsync();
    }
}