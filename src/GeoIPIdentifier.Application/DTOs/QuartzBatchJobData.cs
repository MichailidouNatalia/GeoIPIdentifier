namespace GeoIPIdentifier.Application.DTOs;

public record BatchJobData(string BatchId, List<string> IpAddresses, DateTime CreatedAt);