namespace GeoIPIdentifier.Application.DTOs;

public record BatchGeoIPRequestDto(List<string> IPAddresses);