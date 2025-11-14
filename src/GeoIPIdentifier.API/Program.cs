using GeoIPIdentifier.Adapters.Clients;
using GeoIPIdentifier.Adapters.DataAccess;
using GeoIPIdentifier.Adapters.GatewayIntegration.IPBase.Mappings;
using GeoIPIdentifier.Adapters.Repositories;
using GeoIPIdentifier.Adapters.Services;
using GeoIPIdentifier.API.Middleware;
using GeoIPIdentifier.Application.Interfaces;
using GeoIPIdentifier.Application.Jobs;
using GeoIPIdentifier.Application.Mappings;
using GeoIPIdentifier.Application.Services;
using GeoIPIdentifier.Shared.Interfaces;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
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
        typeof(GeoIPMappingProfile).Assembly,
        typeof(IPBaseMappingProfile).Assembly
    //,typeof(Adapters.Mappings.InfrastructureMappingProfile).Assembly);
    );


    builder.Services.AddHealthChecks()
    .Services
      .AddSqlServer<ApplicationDbContext>(
        builder.Configuration.GetConnectionString("DefaultConnection"))
      .AddStackExchangeRedisCache(
        builder.Configuration.GetValue<string>("Redis:ConnectionString"));

    // Add health checks
    builder.Services.AddHealthChecks()
    .AddSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")!)
    .AddRedis(builder.Configuration.GetConnectionString("Redis")!);; 

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