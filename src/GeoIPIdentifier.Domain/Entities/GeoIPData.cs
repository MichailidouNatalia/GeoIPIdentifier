namespace GeoIPIdentifier.Domain.Entities;

public class GeoIPData : Entity
{
    public string IPAddress { get; private set; }
    public string CountryCode { get; private set; }
    public string CountryName { get; private set; }
    public string Region { get; private set; }
    public string City { get; private set; }
    public decimal Latitude { get; private set; }
    public decimal Longitude { get; private set; }
    public string Timezone { get; private set; }
    public bool IsFromCache { get; private set; }

    private GeoIPData() { }

    public static GeoIPData Create(
        string ipAddress,
        string countryCode,
        string countryName,
        string region,
        string city,
        decimal latitude,
        decimal longitude,
        string timezone,
        bool isFromCache = false)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
        //TODO: Replace with proper domain exception
            //throw new DomainException("IP address is required");
            throw new ArgumentException("IP address is required", nameof(ipAddress));

        return new GeoIPData
        {
            Id = Guid.NewGuid(),
            IPAddress = ipAddress,
            CountryCode = countryCode,
            CountryName = countryName,
            Region = region,
            City = city,
            Latitude = latitude,
            Longitude = longitude,
            Timezone = timezone,
            IsFromCache = isFromCache,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkAsCached()
    {
        IsFromCache = true;
        UpdatedAt = DateTime.UtcNow;
    }
}
