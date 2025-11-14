using GeoIPIdentifier.Application.DTOs;

namespace GeoIPIdentifier.Application.Interfaces;

public interface IGeoIPService
{
  Task<GeoIPResponseDto> IdentifyIPAsync(string ipAddress);
  Task<string> StartBatchProcessingAsync(List<string> ipAddresses);
  Task<BatchProgressResponse> GetBatchProgressAsync(string batchId);
  Task ProcessBatchAsync(string batchId, List<string> ipAddresses);
}