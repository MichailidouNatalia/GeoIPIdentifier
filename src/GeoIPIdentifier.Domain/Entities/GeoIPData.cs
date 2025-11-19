namespace GeoIPIdentifier.Domain.Entities;

public class GeoIPData : Entity
{
  public string IPAddress { get; set; }
  public string CountryCode { get; set; }
  public string CountryName { get; set; }
  public decimal Latitude { get; set; }
  public decimal Longitude { get; set; }
  public string Timezone { get; set; }

  public GeoIPData() { }

}
