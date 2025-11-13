using AutoMapper;
using GeoIPIdentifier.Application.DTOs;
using GeoIPIdentifier.Domain.Entities;

namespace GeoIPIdentifier.Application.Mappings;

public class GeoIPMappingProfile : Profile
{
    public GeoIPMappingProfile()
    {
        CreateMap<GeoIPData, GeoIPResponseDto>();
    }
}