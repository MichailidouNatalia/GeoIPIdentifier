using GeoIPIdentifier.Adapters.Clients;
using GeoIPIdentifier.Adapters.DataAccess;
using GeoIPIdentifier.Adapters.Repositories;
using GeoIPIdentifier.Adapters.Services;
using GeoIPIdentifier.Application.Interfaces;
using GeoIPIdentifier.Application.Mappers;
using GeoIPIdentifier.Application.Services;
using GeoIPIdentifier.Shared.Interfaces;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace GeoIPIdentifier.API;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();

        // Database
        builder.Services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

        // Redis
        builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
            ConnectionMultiplexer.Connect(builder.Configuration.GetValue<string>("Redis:ConnectionString")!));

        // Application Services
        builder.Services.AddScoped<IGeoIPService, GeoIPService>();
        builder.Services.AddScoped<IExternalGeoIPService, IPBaseGateway>();

        // Infrastructure Services & Repositories
        builder.Services.AddScoped<IGeoIPRepository, GeoIPRepository>();
        builder.Services.AddScoped<ICacheService, RedisCacheService>();

        // HTTP Client for external GeoIP service
        builder.Services.AddHttpClient<IPBaseGateway>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "GeoIPIdentifier/1.0");
        });

        // AutoMapper
        builder.Services.AddAutoMapper(
            typeof(GeoIPMappingProfile).Assembly
        //,typeof(Adapters.Mappings.InfrastructureMappingProfile).Assembly);
        );
        var app = builder.Build();

        // Configure pipeline
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();

            // Initialize database
            using var scope = app.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        app.UseHttpsRedirection();
        app.UseAuthorization();
        app.MapControllers();

        await app.RunAsync();
    }
}