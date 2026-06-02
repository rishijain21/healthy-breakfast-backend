using Sovva.Application.Interfaces;
using Sovva.WebAPI.Infrastructure;
using Sovva.WebAPI.Middleware;
using Hangfire;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;

namespace Sovva.WebAPI.Extensions;

/// <summary>
/// Extension methods that configure the HTTP middleware pipeline and endpoints.
/// Extracted from Program.cs for maintainability (ARCH-03).
/// </summary>
public static class WebApplicationExtensions
{
    /// <summary>
    /// Configures the full HTTP middleware pipeline in the correct order.
    /// </summary>
    public static WebApplication UseAppMiddleware(this WebApplication app)
    {
        // Middleware order matters — do not rearrange.
        // CORS must be first so that Access-Control-Allow-Origin headers are present
        // on ALL responses, including error responses from GlobalExceptionMiddleware.
        // Without this, the browser sees missing CORS headers on errors and reports
        // a network/CORS failure instead of the real HTTP status code.
        app.UseCors("CorsPolicy");                       // 1. CORS headers on every response (must be outermost)
        app.UseMiddleware<GlobalExceptionMiddleware>();  // 2. catch all errors (with CORS headers already set)
        app.UseMiddleware<CorrelationIdMiddleware>();     // 3. add correlation ID for tracing
        app.UseSerilogRequestLogging();                   // 4. request logs
        app.UseResponseCompression();                     // 5. compress
        app.UseRateLimiter();                             // 6. rate limit

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Sovva API v1");
                c.RoutePrefix = "swagger";
            });
        }

        app.UseAuthentication();
        app.UseMiddleware<AuthMiddleware>();
        app.UseAuthorization();

        return app;
    }

    /// <summary>
    /// Maps the Hangfire dashboard with admin JWT authorization.
    /// </summary>
    public static WebApplication UseHangfireDashboardWithAuth(this WebApplication app)
    {
        app.MapHangfireDashboard("/hangfire", new DashboardOptions
        {
            Authorization = new[]
            {
                new HangfireAdminAuthorizationFilter()
            },
            DashboardTitle = "Sovva Jobs"
        });

        return app;
    }

    /// <summary>
    /// Maps all application endpoints: controllers, health checks, root, and ping.
    /// </summary>
    public static WebApplication MapAppEndpoints(this WebApplication app)
    {
        app.MapControllers();

        app.MapGet("/", () => new
        {
            service = "Sovva API",
            version = "1.0",
            status = "Running",
            environment = app.Environment.EnvironmentName,
            timestamp = DateTime.UtcNow
        });

        app.MapGet("/ping", () => Results.Ok("pong"));

        // Liveness — is the process alive? (fast, no DB)
        app.MapHealthChecks("/health/live", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("live"),
            ResponseWriter = WriteHealthResponse
        });

        // Readiness — is the DB reachable? (used by load balancers)
        app.MapHealthChecks("/health/ready", new HealthCheckOptions
        {
            Predicate = check => check.Tags.Contains("ready") || check.Tags.Contains("live"),
            ResponseWriter = WriteHealthResponse
        });

        // Combined (backward compat)
        app.MapHealthChecks("/health", new HealthCheckOptions
        {
            ResponseWriter = WriteHealthResponse
        });

        return app;
    }

    /// <summary>
    /// Registers all recurring Hangfire jobs using IST timezone.
    /// </summary>
    public static WebApplication ScheduleHangfireJobs(this WebApplication app)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();

        // Global Hangfire retry filter
        GlobalJobFilters.Filters.Add(new AutomaticRetryAttribute { Attempts = 3 });

        // Register failure alert filter
        GlobalJobFilters.Filters.Add(app.Services.GetRequiredService<JobFailureAlertFilter>());

        try
        {
            var istZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata");

            // 1. Expire old subscriptions — runs first, clean slate
            RecurringJob.AddOrUpdate<ISubscriptionService>(
                "expire-subscriptions",
                s => s.ExpireSubscriptionsAsync(),
                "50 23 * * *",
                new RecurringJobOptions { TimeZone = istZone });

            // 2. Sync subscription dates — safety net
            RecurringJob.AddOrUpdate<ISubscriptionService>(
                "sync-subscription-dates",
                s => s.UpdateNextScheduledDatesAsync(),
                "55 23 * * *",
                new RecurringJobOptions { TimeZone = istZone });

            // 3. Confirm today's orders AT midnight — wallet deducted, Orders row created
            RecurringJob.AddOrUpdate<IScheduledOrderService>(
                "midnight-order-confirmation",
                s => s.ConfirmAllScheduledOrdersAsync(null),
                "0 0 * * *",
                new RecurringJobOptions { TimeZone = istZone });

            // 4. Generate next-day subscription orders at 12:01 AM IST
            RecurringJob.AddOrUpdate<ISubscriptionSchedulingService>(
                "subscription-order-generation",
                s => s.GenerateScheduledOrdersFromSubscriptionsAsync(),
                "1 0 * * *",
                new RecurringJobOptions { TimeZone = istZone });

            logger.LogInformation("Hangfire jobs scheduled successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to schedule Hangfire jobs");
        }

        logger.LogInformation("Sovva API started | Env: {Env}",
            app.Environment.EnvironmentName);

        return app;
    }

    /// <summary>
    /// Health check response writer — returns JSON with status, timestamp, and per-check details.
    /// </summary>
    private static Task WriteHealthResponse(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        var response = new
        {
            status = report.Status.ToString(),
            timestamp = DateTime.UtcNow,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration = e.Value.Duration.TotalMilliseconds + "ms"
            })
        };
        return context.Response.WriteAsync(
            System.Text.Json.JsonSerializer.Serialize(response));
    }
}
