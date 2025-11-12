using GeoIPIdentifier.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace GeoIPIdentifier.Adapters.DataAccess;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

    public DbSet<GeoIPData> GeoIPData => Set<GeoIPData>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }
}