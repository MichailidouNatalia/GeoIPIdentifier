namespace GeoIPIdentifier.Application.DTOs;

public record GeoIPResponseDto(
    Guid Id,
    string IPAddress,
    string CountryCode,
    string CountryName,
    string Region,
    string City,
    decimal Latitude,
    decimal Longitude,
    string Timezone,
    bool IsFromCache,
    DateTime CreatedAt);

