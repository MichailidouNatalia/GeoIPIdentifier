using GeoIPIdentifier.Adapters.DataAccess;
using GeoIPIdentifier.Adapters.DataAccess.CacheService;
using GeoIPIdentifier.Adapters.DataAccess.Repositories;
using GeoIPIdentifier.Adapters.GatewayIntegration.IPBase.Client;
using GeoIPIdentifier.Adapters.GatewayIntegration.IPBase.Mappings;
using GeoIPIdentifier.API.Middleware;
using GeoIPIdentifier.Application.Interfaces;
using GeoIPIdentifier.Application.Jobs;
using GeoIPIdentifier.Application.Mappings;
using GeoIPIdentifier.Application.Services;
using GeoIPIdentifier.Shared.Interfaces;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Newtonsoft.Json;
using Quartz;
using Quartz.AspNetCore;
using StackExchange.Redis;

namespace GeoIPIdentifier.API;

public class Program
{
  public static async Task Main(string[] args)
  {
    var builder = WebApplication.CreateBuilder(args);

    // Add services
    builder.Services.AddControllers()
    .AddNewtonsoftJson(options =>
{
  options.SerializerSettings.NullValueHandling = NullValueHandling.Ignore;
  options.SerializerSettings.DateTimeZoneHandling = DateTimeZoneHandling.Utc;
});
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    // Database
    builder.Services.AddDbContext<ApplicationDbContext>(options =>
        options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

    // Redis
    builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
        ConnectionMultiplexer.Connect(builder.Configuration.GetValue<string>("Redis:ConnectionString")!));

    // Quartz Configuration
    builder.Services.AddQuartz(q =>
{
  // Base scheduler configuration
  q.SchedulerId = "GeoIP-Scheduler";
  q.SchedulerName = "GeoIP Batch Processing Scheduler";

  // Set thread pool settings
  q.UseDefaultThreadPool(tp =>
  {
    tp.MaxConcurrency = 10;
  });

  // Job configuration
  q.AddJob<BatchGeoIPJob>(j => j
          .WithIdentity("BatchGeoIPJob", "geoip-batches")
          .StoreDurably()
      );
});

    // Register Quartz server
    builder.Services.AddQuartzServer(options =>
    {
      options.WaitForJobsToComplete = true;
      options.AwaitApplicationStarted = true;
    });

    // Application Services
    builder.Services.AddScoped<IGeoIPService, GeoIPService>();
    builder.Services.AddScoped<IIPBaseClient, IPBaseGateway>();
    builder.Services.AddScoped<IBatchJobScheduler, BatchJobScheduler>();

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
        typeof(GeoIPMappingProfile).Assembly,
        typeof(IPBaseMappingProfile).Assembly
    //,typeof(Adapters.Mappings.InfrastructureMappingProfile).Assembly);
    );

    // Add health checks
    builder.Services.AddHealthChecks()
        .AddSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")!,
                      healthQuery: "SELECT 1;",
                      configure: null,
                      name: "sqlserver",
                      failureStatus: HealthStatus.Unhealthy,
                      tags: ["database", "ready"])
        .AddRedis(
            builder.Configuration.GetValue<string>("Redis:ConnectionString")!,
            name: "Redis",
            tags: ["ready"]);

    var app = builder.Build();

    // Add health check endpoint
    app.MapHealthChecks("/health");
    app.MapHealthChecks("/health/ready", new HealthCheckOptions
    {
      Predicate = check => check.Tags.Contains("ready")
    });
    app.MapHealthChecks("/health/live", new HealthCheckOptions
    {
      Predicate = _ => false
    });

    // Middleware
    app.UseMiddleware<ExceptionMiddleware>();


    app.UseSwagger();
    app.UseSwaggerUI();

    // Initialize database
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await dbContext.Database.EnsureCreatedAsync();
    await dbContext.Database.MigrateAsync();


    app.UseHttpsRedirection();
    app.UseAuthorization();
    app.MapControllers();

    await app.RunAsync();
  }
}