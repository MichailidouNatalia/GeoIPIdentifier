using System.Net.Http.Json;
using GeoIPIdentifier.Application.Interfaces;
using GeoIPIdentifier.Domain.Entities;

namespace GeoIPIdentifier.Adapters.Clients;
public class IPBaseGateway : IExternalGeoIPService
{
    private readonly HttpClient _httpClient;

    public IPBaseGateway(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<GeoIPData> GetGeoIPDataAsync(string ipAddress)
    {
        // Using ipapi.co as example - you can replace with any GeoIP service
        var response = await _httpClient.GetFromJsonAsync<IpApiResponse>($"http://ipapi.co/{ipAddress}/json/");
        
        return GeoIPData.Create(
            ipAddress,
            response?.CountryCode ?? "Unknown",
            response?.CountryName ?? "Unknown",
            response?.Region ?? "Unknown",
            response?.City ?? "Unknown",
            response?.Latitude ?? 0,
            response?.Longitude ?? 0,
            response?.Timezone ?? "Unknown");
    }

    private class IpApiResponse
    {
        public string? CountryCode { get; set; }
        public string? CountryName { get; set; }
        public string? Region { get; set; }
        public string? City { get; set; }
        public decimal Latitude { get; set; }
        public decimal Longitude { get; set; }
        public string? Timezone { get; set; }
    }
}