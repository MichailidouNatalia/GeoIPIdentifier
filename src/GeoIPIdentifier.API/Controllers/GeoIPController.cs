using Microsoft.AspNetCore.Mvc;
using GeoIPIdentifier.Application.DTOs;
using GeoIPIdentifier.Application.Interfaces;

namespace GeoIPIdentifier.API.Controllers;

[ApiController]
[Route("api/geoip")]
public class GeoIPController : ControllerBase
{
    private readonly IGeoIPService _geoIPService;

    public GeoIPController(IGeoIPService geoIPService)
    {
        _geoIPService = geoIPService;
    }

    [HttpGet("{ipAddress}")]
    public async Task<ActionResult<GeoIPResponseDto>> IdentifyIP(string ipAddress)
    {
        var result = await _geoIPService.IdentifyIPAsync(ipAddress);
        return Ok(result);
    }

    [HttpGet("history")]
    public async Task<ActionResult<IEnumerable<GeoIPResponseDto>>> GetHistory()
    {
        var result = await _geoIPService.GetHistoryAsync();
        return Ok(result);
    }
}