namespace GeoIPIdentifier.Application.DTOs;

public record BatchProgressResponse(
    string BatchId,
    string Status,
    int Total,
    int Processed,
    DateTime StartTime,
    DateTime? CompletedTime)
{
  public double ProgressPercentage => Total > 0 ? (Processed / (double)Total) * 100 : 0;
  public TimeSpan? EstimatedTimeRemaining { get; init; }
}