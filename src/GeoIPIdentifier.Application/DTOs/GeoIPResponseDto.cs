namespace GeoIPIdentifier.Application.DTOs;

public record GeoIPResponseDto(
    Guid Id,
    string IPAddress,
    string CountryCode,
    string CountryName,
    decimal Latitude,
    decimal Longitude,
    string Timezone);

