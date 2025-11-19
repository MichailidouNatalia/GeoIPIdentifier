using GeoIPIdentifier.Domain.Entities;

namespace GeoIPIdentifier.Application.Interfaces;

public interface IIPBaseClient
{
  Task<GeoIPData> GetGeoIPDataAsync(string ipAddress);
}