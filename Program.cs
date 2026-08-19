using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using NHN.Power.API.Middleware;
using NHN.Power.API.Models;
using NHN.Power.API.Infrastructure;
using NHN.Power.API.Repositories;
using NHN.Power.API.Services;

using Azure.Core.Serialization;
using System.Text.Json;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

builder.Services.Configure<WorkerOptions>(workerOptions =>
{
    workerOptions.Serializer = new JsonObjectSerializer(
        new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        }
    );
});

// Configure Middleware
builder.UseMiddleware<ExceptionHandlingMiddleware>();

// Configuration
builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("AppSettings"));

var appSettings = builder.Configuration.GetSection("AppSettings").Get<AppSettings>();
if (appSettings != null)
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = appSettings.JwtIssuer,
                ValidAudience = appSettings.JwtAudience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(appSettings.JwtSecret ?? ""))
            };
        });
    builder.Services.AddAuthorization();
}

// Services
builder.Services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();
builder.Services.AddSingleton<IJwtService, JwtService>();
builder.Services.AddSingleton<IPasswordService, PasswordService>();
builder.Services.AddSingleton<IPricingValidationService, PricingValidationService>();
builder.Services.AddSingleton<IStorageService, StorageService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IQuoteRepository, QuoteRepository>();
builder.Services.AddScoped<ITrackingRepository, TrackingRepository>();

// (Optional) Remove OpenTelemetry for now to avoid telemetry setup issues, 
// or keep it if AzureMonitor is needed in V1. We can keep it simple for now.

builder.Build().Run();
