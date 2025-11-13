using GeoIPIdentifier.Adapters.GatewayIntegration.IPBase.DTOs;

namespace GeoIPIdentifier.Adapters.GatewayIntegration.IPBase.Client;
public interface IIPBaseClient
{
    Task<IPBaseResponseDto> GetGeoIPDataAsync(string ipAddress);
}
