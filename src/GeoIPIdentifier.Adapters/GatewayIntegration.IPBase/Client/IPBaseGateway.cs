using Newtonsoft.Json;
using GeoIPIdentifier.Application.Interfaces;
using GeoIPIdentifier.Domain.Entities;
using GeoIPIdentifier.Domain.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using GeoIPIdentifier.Adapters.GatewayIntegration.IPBase.DTOs;
using AutoMapper;

namespace GeoIPIdentifier.Adapters.GatewayIntegration.IPBase.Client;

public class IPBaseGateway : IIPBaseClient
{
  private readonly HttpClient _httpClient;
  private readonly IConfiguration _configuration;
  private readonly ILogger<IPBaseGateway> _logger;
  private readonly IMapper _mapper;

  public IPBaseGateway(
      HttpClient httpClient,
      IConfiguration configuration,
      ILogger<IPBaseGateway> logger,
      IMapper mapper)
  {
    _httpClient = httpClient;
    _configuration = configuration;
    _logger = logger;
    _mapper = mapper;
  }

  public async Task<GeoIPData> GetGeoIPDataAsync(string ipAddress)
  {
    try
    {
      var apiKey = _configuration["IPBase:ApiKey"];
      if (string.IsNullOrEmpty(apiKey))
      {
        throw new DomainException("IPBase API key is not configured");
      }

      var url = $"https://api.ipbase.com/v2/info?ip={ipAddress}";

      var request = new HttpRequestMessage(HttpMethod.Get, url);
      request.Headers.Add("apikey", apiKey);

      _logger.LogInformation("Calling IPBase API for IP: {IPAddress}", ipAddress);

      var response = await _httpClient.SendAsync(request);

      if (!response.IsSuccessStatusCode)
      {
        await HandleApiError(response, ipAddress);
      }

      var content = await response.Content.ReadAsStringAsync();
      var ipBaseResponse = JsonConvert.DeserializeObject<IPBaseResponseDto>(content);

      if (ipBaseResponse?.Data == null)
      {
        throw new DomainException("Invalid response from IPBase API");
      }

      return _mapper.Map<GeoIPData>(ipBaseResponse.Data);
    }
    catch (HttpRequestException ex)
    {
      _logger.LogError(ex, "Network error calling IPBase API for IP: {IPAddress}", ipAddress);
      throw new GeoIPServiceUnavailableException();
    }
    catch (JsonException ex)
    {
      _logger.LogError(ex, "JSON deserialization error for IP: {IPAddress}", ipAddress);
      throw new DomainException("Invalid response format from GeoIP service");
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Unexpected error calling IPBase API for IP: {IPAddress}", ipAddress);
      throw;
    }
  }

  private async Task HandleApiError(HttpResponseMessage response, string ipAddress)
  {
    var statusCode = (int)response.StatusCode;
    var errorContent = await response.Content.ReadAsStringAsync();

    _logger.LogWarning(
        "IPBase API returned error status {StatusCode} for IP {IPAddress}. Response: {ErrorContent}",
        statusCode, ipAddress, errorContent);

    switch (statusCode)
    {
      case 400:
        throw new InvalidIPAddressException(ipAddress);
      case 401:
      case 403:
        throw new DomainException("Invalid or missing IPBase API key");
      case 429:
        throw new DomainException("Rate limit exceeded for IPBase API");
      case 500:
      case 502:
      case 503:
        throw new GeoIPServiceUnavailableException();
      default:
        throw new DomainException($"IPBase API error: {statusCode}");
    }
  }

}