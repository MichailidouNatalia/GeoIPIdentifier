using GeoIPIdentifier.Domain.Entities;

namespace GeoIPIdentifier.Application.Interfaces;

public interface IExternalGeoIPService
{
  Task<GeoIPData> GetGeoIPDataAsync(string ipAddress);
}