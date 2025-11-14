
using GeoIPIdentifier.Domain.Entities;

namespace GeoIPIdentifier.Application.Interfaces;

public interface IGeoIPRepository
{
  Task<GeoIPData?> GetByIPAsync(string ipAddress);
  Task AddAsync(GeoIPData geoIPData);
  Task<IEnumerable<GeoIPData>> GetRecentAsync(int count = 10);
}