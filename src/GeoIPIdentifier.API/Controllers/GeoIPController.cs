using Microsoft.AspNetCore.Mvc;
using GeoIPIdentifier.Application.DTOs;
using GeoIPIdentifier.Application.Interfaces;

namespace GeoIPIdentifier.API.Controllers;

[ApiController]
[Route("api/geoip", Name = "GeoIPInfo")]
public class GeoIPController(IGeoIPService geoIPService, ILogger<GeoIPController> logger) : ControllerBase
{
  private readonly IGeoIPService _geoIPService = geoIPService;
  private readonly ILogger<GeoIPController> _logger = logger;

  [HttpGet("{ipAddress}")]
  public async Task<ActionResult<GeoIPResponseDto>> IdentifyIP(string ipAddress)
  {
    var result = await _geoIPService.IdentifyIPAsync(ipAddress);
    return Ok(result);
  }

  [HttpPost("batch", Name = "BatchGeolocate")]
  public async Task<ActionResult<BatchGeoIPResponseDto>> BatchGeolocate(BatchGeoIPRequestDto request)
  {
    try
    {
      if (request.IPAddresses == null || request.IPAddresses.Count == 0)
        return BadRequest("At least one IP address is required.");

      if (request.IPAddresses.Count > 1000)
        return BadRequest("Maximum 1000 IP addresses allowed per batch.");

      var batchId = await _geoIPService.StartBatchProcessingAsync(request.IPAddresses);

      var progressUrl = Url.RouteUrl("GetProgress", new { id = batchId }, Request.Scheme)!;

      _logger.LogInformation("Started batch {BatchId} with {Count} IPs", batchId, request.IPAddresses.Count);

      return Ok(new BatchGeoIPResponseDto(
          BatchId: batchId,
          ProgressUrl: progressUrl
      ));
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error starting batch processing");
      return StatusCode(500, "An error occurred while starting batch processing.");
    }
  }

  [HttpGet("/batch/{id}", Name = "GetProgress")]
  public async Task<ActionResult<BatchProgressResponse>> GetProgress(string id)
  {
    try
    {
      var progress = await _geoIPService.GetBatchProgressAsync(id);
      if (progress == null)
        return NotFound($"Batch ID {id} not found or expired.");

      return Ok(progress);
    }
    catch (Exception ex)
    {
      _logger.LogError(ex, "Error getting progress for batch {BatchId}", id);
      return StatusCode(500, "An error occurred while retrieving progress.");
    }
  }
}