using Serilog;
using Sovva.WebAPI.Extensions;

// IPv6 switch removed because Supabase direct connection is IPv6-only.

// ══════════════════════════════════════════════════
// LOGGING
// ══════════════════════════════════════════════════
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseShutdownTimeout(TimeSpan.FromSeconds(30));

var loggerConfig = new LoggerConfiguration()
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
                        "[{Level:u3}] {Message:lj}{NewLine}{Exception}");

var seqUrl = builder.Configuration["Logging:SeqUrl"];
if (!string.IsNullOrWhiteSpace(seqUrl))
{
    loggerConfig.WriteTo.Seq(serverUrl: seqUrl, apiKey: builder.Configuration["Logging:SeqApiKey"]);
}

Log.Logger = loggerConfig.CreateLogger();

builder.Host.UseSerilog();

// ══════════════════════════════════════════════════
// SERVICES
// ══════════════════════════════════════════════════
builder.Services
    .AddAppConfiguration(builder.Configuration)
    .AddDatabase(builder.Configuration)
    .AddHangfireServices(builder.Configuration)
    .AddApplicationServices()
    .AddAppCaching(builder.Configuration)
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

// Configuration checks removed to allow Render's DATABASE_URL mapping in ServiceCollectionExtensions

app.UseAppMiddleware()
   .UseHangfireDashboardWithAuth()
   .MapAppEndpoints()
   .ScheduleHangfireJobs();

app.Run();
