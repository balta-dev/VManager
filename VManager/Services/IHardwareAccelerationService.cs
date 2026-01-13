using System.Collections.Generic;
using System.Threading.Tasks;
using VManager.Services.Models;

namespace VManager.Services
{
    public interface IHardwareAccelerationService
    {
        Task<IReadOnlyList<string>> GetAvailableVideoCodecsAsync();
        Task<IReadOnlyList<string>> GetAvailableAudioCodecsAsync();
        Task<HardwareCapabilities> GetHardwareCapabilitiesAsync();
    }
}