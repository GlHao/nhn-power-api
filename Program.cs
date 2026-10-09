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
using Microsoft.EntityFrameworkCore;

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
builder.Services.AddHttpClient<ISmsService, ClickSendSmsService>();
builder.Services.AddScoped<INotificationService, NotificationService>();

builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IQuoteRepository, QuoteRepository>();
builder.Services.AddScoped<ITrackingRepository, TrackingRepository>();

// NHN Biz Repositories & Services
builder.Services.AddScoped<IEmployeeRepository, EmployeeRepository>();
builder.Services.AddScoped<IEmployeeService, EmployeeService>();
builder.Services.AddScoped<IClientRepository, ClientRepository>();
builder.Services.AddScoped<IClientService, ClientService>();
builder.Services.AddScoped<ISiteRepository, SiteRepository>();
builder.Services.AddScoped<ISiteService, SiteService>();
builder.Services.AddScoped<IBillingRuleRepository, BillingRuleRepository>();
builder.Services.AddScoped<IBillingRuleService, BillingRuleService>();
builder.Services.AddScoped<IScheduleRuleRepository, ScheduleRuleRepository>();
builder.Services.AddScoped<IScheduleRuleService, ScheduleRuleService>();
builder.Services.AddScoped<IPublicHolidayHelper, PublicHolidayHelper>();
builder.Services.AddScoped<IScheduleCalculator, ScheduleCalculator>();
builder.Services.AddScoped<IBillingCalculator, BillingCalculator>();
builder.Services.AddScoped<ITaxInvoicePreviewService, TaxInvoicePreviewService>();
builder.Services.AddScoped<ITaxInvoicePdfService, TaxInvoicePdfService>();
builder.Services.AddScoped<IPayrollCalculatorService, PayrollCalculatorService>();
builder.Services.AddScoped<IEmployeePaySlipPdfService, EmployeePaySlipPdfService>();

// V3 Receipts, Ledger & Todo DI Registrations
builder.Services.AddScoped<IAzureDocumentIntelligenceService, AzureDocumentIntelligenceService>();
builder.Services.AddScoped<IReceiptRepository, ReceiptRepository>();
builder.Services.AddScoped<IReceiptService, ReceiptService>();
builder.Services.AddScoped<ILedgerRepository, LedgerRepository>();
builder.Services.AddScoped<ILedgerService, LedgerService>();
builder.Services.AddScoped<ITodoRepository, TodoRepository>();
builder.Services.AddScoped<ITodoService, TodoService>();

QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

builder.Services.AddDbContext<NHN.Power.API.Data.AppDbContext>((serviceProvider, options) =>
{
    var configuration = serviceProvider.GetRequiredService<IConfiguration>();
    var connectionString = Environment.GetEnvironmentVariable("SUPABASE_CONNECTION_STRING");
    if (string.IsNullOrEmpty(connectionString))
    {
        connectionString = configuration["AppSettings:SupabaseConnectionString"] 
                        ?? configuration.GetConnectionString("SupabaseConnectionString")
                        ?? configuration["SUPABASE_CONNECTION_STRING"];
    }

    var validConnString = NHN.Power.API.Data.ConnectionStringHelper.GetValidConnectionString(connectionString);
    options.UseNpgsql(validConnString);
});

builder.Build().Run();
