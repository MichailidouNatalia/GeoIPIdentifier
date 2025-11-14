using AutoMapper;
using GeoIPIdentifier.Adapters.GatewayIntegration.IPBase.DTOs;
using GeoIPIdentifier.Domain.Entities;

namespace GeoIPIdentifier.Adapters.GatewayIntegration.IPBase.Mappings;

public class IPBaseMappingProfile : Profile
{
  public IPBaseMappingProfile()
  {
    CreateMap<IPBaseData, GeoIPData>()
        .ForMember(dest => dest.IPAddress, opt => opt.MapFrom(src => src.IP))
        .ForMember(dest => dest.CountryCode, opt => opt.MapFrom(src => src.Location.Country.Alpha2 ?? "Unknown"))
        .ForMember(dest => dest.CountryName, opt => opt.MapFrom(src => src.Location.Country.Name ?? "Unknown"))
        .ForMember(dest => dest.Latitude, opt => opt.MapFrom(src => src.Location.Latitude ?? 0))
        .ForMember(dest => dest.Longitude, opt => opt.MapFrom(src => src.Location.Longitude ?? 0))
        .ForMember(dest => dest.Timezone, opt => opt.MapFrom(src => src.Timezone.Code ?? "Unknown"))
        .ForMember(dest => dest.IsFromCache, opt => opt.Ignore())
        .ForMember(dest => dest.Id, opt => opt.Ignore())
        .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
        .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore());
  }
}