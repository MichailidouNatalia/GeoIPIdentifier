using Newtonsoft.Json;

namespace GeoIPIdentifier.Adapters.GatewayIntegration.IPBase.DTOs;

public class IPBaseResponseDto
{
  [JsonProperty("data")]
  public IPBaseData Data { get; set; }
}

public class IPBaseData
{
  [JsonProperty("ip")]
  public string IP { get; set; }

  [JsonProperty("location")]
  public Location Location { get; set; }

  [JsonProperty("timezone")]
  public Timezone Timezone { get; set; }
}
public class Location
{

  [JsonProperty("latitude")]
  public decimal? Latitude { get; set; }

  [JsonProperty("longitude")]
  public decimal? Longitude { get; set; }

  [JsonProperty("country")]
  public Country Country { get; set; }
}

public class Country
{
  [JsonProperty("alpha2")]
  public string Alpha2 { get; set; }

  [JsonProperty("name")]
  public string Name { get; set; }
}


public class Timezone
{
  [JsonProperty("code")]
  public string Code { get; set; }
}