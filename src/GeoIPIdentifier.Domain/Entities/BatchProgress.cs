namespace GeoIPIdentifier.Domain.Entities;

public class BatchProgress
{
  public string BatchId { get; set; } = string.Empty;
  public string Status { get; set; } = "Queued";
  public int Total { get; set; }
  public int Processed { get; set; }
  public DateTime StartTime { get; set; }
  public DateTime? CompletedTime { get; set; }
}