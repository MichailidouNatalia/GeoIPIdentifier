using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using GeoIPIdentifier.Domain.Entities;

namespace GeoIPIdentifier.Adapters.DataAccess.EntityConfigs;
public class GeoIPDataConfiguration : IEntityTypeConfiguration<GeoIPData>
{
    public void Configure(EntityTypeBuilder<GeoIPData> builder)
    {
        builder.HasKey(x => x.Id);
        
        builder.Property(x => x.IPAddress)
            .IsRequired()
            .HasMaxLength(45);
            
        builder.Property(x => x.CountryCode)
            .HasMaxLength(5);

        builder.Property(x => x.CountryName)
            .HasMaxLength(100);
    
            
        builder.Property(x => x.Timezone)
            .HasMaxLength(50);

        builder.HasIndex(x => x.IPAddress);
        builder.HasIndex(x => x.CreatedAt);
    }
}