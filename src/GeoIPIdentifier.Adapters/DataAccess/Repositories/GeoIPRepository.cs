using Microsoft.EntityFrameworkCore;
using GeoIPIdentifier.Domain.Entities;
using GeoIPIdentifier.Adapters.DataAccess;
using GeoIPIdentifier.Application.Interfaces;

namespace GeoIPIdentifier.Adapters.DataAccess.Repositories;

public class GeoIPRepository : IGeoIPRepository
{
  private readonly ApplicationDbContext _context;

  public GeoIPRepository(ApplicationDbContext context)
  {
    _context = context;
  }

  public async Task<GeoIPData?> GetByIPAsync(string ipAddress)
  {
    return await _context.GeoIPData
        .Where(x => x.IPAddress == ipAddress)
        .OrderByDescending(x => x.CreatedAt)
        .FirstOrDefaultAsync();
  }

  public async Task AddAsync(GeoIPData geoIPData)
  {
    await _context.GeoIPData.AddAsync(geoIPData);
    await _context.SaveChangesAsync();
  }

  public async Task<IEnumerable<GeoIPData>> GetRecentAsync(int count = 10)
  {
    return await _context.GeoIPData
        .OrderByDescending(x => x.CreatedAt)
        .Take(count)
        .ToListAsync();
  }

  public async Task<GeoIPData?> GetByIdAsync(Guid id)
  {
    return await _context.GeoIPData.FindAsync(id);
  }

  public async Task UpdateAsync(GeoIPData geoIPData)
  {
    _context.GeoIPData.Update(geoIPData);
    await _context.SaveChangesAsync();
  }

  public async Task DeleteAsync(Guid id)
  {
    var entity = await GetByIdAsync(id);
    if (entity != null)
    {
      _context.GeoIPData.Remove(entity);
      await _context.SaveChangesAsync();
    }
  }

  public async Task<IEnumerable<GeoIPData>> GetByCountryAsync(string countryCode)
  {
    return await _context.GeoIPData
        .Where(x => x.CountryCode == countryCode)
        .OrderByDescending(x => x.CreatedAt)
        .ToListAsync();
  }

  public async Task<int> GetTotalRecordsAsync()
  {
    return await _context.GeoIPData.CountAsync();
  }

  public async Task<bool> ExistsAsync(string ipAddress)
  {
    return await _context.GeoIPData
        .AnyAsync(x => x.IPAddress == ipAddress);
  }
}