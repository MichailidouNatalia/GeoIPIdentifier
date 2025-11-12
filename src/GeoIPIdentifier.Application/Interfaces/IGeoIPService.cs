using GeoIPIdentifier.Application.DTOs;

namespace GeoIPIdentifier.Application.Interfaces;

public interface IGeoIPService
{
    Task<GeoIPResponseDto> IdentifyIPAsync(string ipAddress);
    Task<IEnumerable<GeoIPResponseDto>> GetHistoryAsync();
}