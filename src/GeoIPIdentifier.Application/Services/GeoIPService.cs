using AutoMapper;
using GeoIPIdentifier.Application.DTOs;
using GeoIPIdentifier.Application.Interfaces;
using GeoIPIdentifier.Domain.Entities;
using GeoIPIdentifier.Domain.Exceptions;
using GeoIPIdentifier.Shared.Interfaces;
using Microsoft.Extensions.Logging;

namespace GeoIPIdentifier.Application.Services;

public class GeoIPService : IGeoIPService
{
    private readonly IGeoIPRepository _repository;
    private readonly ICacheService _cacheService;
    private readonly IMapper _mapper;
    private readonly IExternalGeoIPService _externalService;
    private readonly ILogger<GeoIPService> _logger;

    public GeoIPService(
        IGeoIPRepository repository,
        ICacheService cacheService,
        IMapper mapper,
        IExternalGeoIPService externalService,
        ILogger<GeoIPService> logger)
    {
        _repository = repository;
        _cacheService = cacheService;
        _mapper = mapper;
        _externalService = externalService;
        _logger = logger;
    }

    public async Task<GeoIPResponseDto> IdentifyIPAsync(string ipAddress)
    {
        if (string.IsNullOrWhiteSpace(ipAddress))
            throw new DomainException("IP address cannot be empty");

        if (!System.Net.IPAddress.TryParse(ipAddress, out _))
            throw new InvalidIPAddressException(ipAddress);

        try
        {
            // Check cache first
            var cacheKey = $"geoip:{ipAddress}";
            var cached = await _cacheService.GetAsync<GeoIPResponseDto>(cacheKey);
            if (cached != null)
                return cached;

            // Check database
            /* var existing = await _repository.GetByIPAsync(ipAddress);
            if (existing != null)
            {
                var response = _mapper.Map<GeoIPResponseDto>(existing);
                await _cacheService.SetAsync(cacheKey, response, TimeSpan.FromHours(1));
                return response;
            } */

            // Call external service
            var geoIPData = await _externalService.GetGeoIPDataAsync(ipAddress);
            //await _repository.AddAsync(geoIPData);

            var result = _mapper.Map<GeoIPResponseDto>(geoIPData);
            await _cacheService.SetAsync(cacheKey, result, TimeSpan.FromHours(1));

            return result;
        }
        catch (HttpRequestException)
        {
            throw new GeoIPServiceUnavailableException();
        }
        catch (Exception ex) when (ex.Message.Contains("rate limit", StringComparison.OrdinalIgnoreCase))
        {
            throw new RateLimitExceededException();
        }
    }

    public async Task<IEnumerable<GeoIPResponseDto>> GetHistoryAsync()
    {
        var recent = await _repository.GetRecentAsync(50);
        return _mapper.Map<IEnumerable<GeoIPResponseDto>>(recent);
    }
}