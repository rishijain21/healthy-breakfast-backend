using Serilog;
using Sovva.WebAPI.Extensions;

// ══════════════════════════════════════════════════
// LOGGING
// ══════════════════════════════════════════════════
var builder = WebApplication.CreateBuilder(args);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Hangfire", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/sovva-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} " +
                        "[{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.Seq(
        serverUrl: builder.Configuration["Logging:SeqUrl"] ?? "http://localhost:5341",
        apiKey: builder.Configuration["Logging:SeqApiKey"])
    .CreateLogger();

builder.Host.UseSerilog();

// ══════════════════════════════════════════════════
// SERVICES
// ══════════════════════════════════════════════════
builder.Services
    .AddAppConfiguration(builder.Configuration)
    .AddDatabase(builder.Configuration)
    .AddHangfireServices(builder.Configuration)
    .AddApplicationServices()
    .AddApiInfrastructure()
    .AddAppCors(builder.Configuration)
    .AddAppRateLimiting()
    .AddAppAuth(builder.Configuration)
    .AddAppSwagger()
    .AddAppHealthChecks(builder.Configuration);

// ══════════════════════════════════════════════════
// APP
// ══════════════════════════════════════════════════
var app = builder.Build();

app.UseAppMiddleware()
   .UseHangfireDashboardWithAuth()
   .MapAppEndpoints()
   .ScheduleHangfireJobs();

app.Run();
