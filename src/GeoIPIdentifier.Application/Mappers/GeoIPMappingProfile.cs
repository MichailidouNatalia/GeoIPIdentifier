using AutoMapper;
using GeoIPIdentifier.Application.DTOs;
using GeoIPIdentifier.Domain.Entities;

namespace GeoIPIdentifier.Application.Mappers;

public class GeoIPMappingProfile : Profile
{
    public GeoIPMappingProfile()
    {
        CreateMap<GeoIPData, GeoIPResponseDto>();
    }
}