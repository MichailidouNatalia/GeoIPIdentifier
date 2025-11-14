namespace GeoIPIdentifier.Domain.Exceptions;

public class DomainException : Exception
{
  public DomainException(string message) : base(message) { }

  public DomainException(string message, Exception innerException)
      : base(message, innerException) { }
}

public class InvalidIPAddressException : DomainException
{
  public InvalidIPAddressException(string ipAddress)
      : base($"Invalid IP address format: {ipAddress}") { }
}

public class GeoIPServiceUnavailableException : DomainException
{
  public GeoIPServiceUnavailableException()
      : base("GeoIP service is temporarily unavailable") { }
}

public class RateLimitExceededException : DomainException
{
  public RateLimitExceededException()
      : base("Rate limit exceeded. Please try again later.") { }
}