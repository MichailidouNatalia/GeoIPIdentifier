using GeoIPIdentifier.Domain.Exceptions;

namespace GeoIPIdentifier.Domain.Entities;

public class GeoIPData : Entity
{
    public string IPAddress { get; private set; }
    public string CountryCode { get; private set; }
    public string CountryName { get; private set; }
    public decimal Latitude { get; private set; }
    public decimal Longitude { get; private set; }
    public string Timezone { get; private set; }
    public bool IsFromCache { get; private set; }

    private GeoIPData() { }

    public static GeoIPData Create(
        string ipAddress,
        string countryCode,
        string countryName,
        decimal latitude,
        decimal longitude,
        string timezone,
        bool isFromCache = false)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
            throw new DomainException("IP address is required");

        if (!IsValidIPAddress(ipAddress))
            throw new InvalidIPAddressException(ipAddress);

        return new GeoIPData
        {
            Id = Guid.NewGuid(),
            IPAddress = ipAddress,
            CountryCode = countryCode,
            CountryName = countryName,
            Latitude = latitude,
            Longitude = longitude,
            Timezone = timezone,
            IsFromCache = isFromCache,
            CreatedAt = DateTime.UtcNow
        };
    }
    
    private static bool IsValidIPAddress(string ipAddress)
    {
        return System.Net.IPAddress.TryParse(ipAddress, out _);
    }

    public void MarkAsCached()
    {
        IsFromCache = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
