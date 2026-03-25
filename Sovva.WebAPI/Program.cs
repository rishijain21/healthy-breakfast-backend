// Sovva.WebAPI/Program.cs

using Serilog;
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;
using Sovva.Application.Services;
using Sovva.Infrastructure.Data;
using Sovva.Infrastructure.Repositories;
using Sovva.WebAPI.Configuration;
using Sovva.WebAPI.Middleware;
using Sovva.WebAPI.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using FluentValidation;
using FluentValidation.AspNetCore;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

// ══════════════════════════════════════════════════
// LOGGING — must be first
// ══════════════════════════════════════════════════
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Hangfire", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    // ✅ REMOVED: .WriteTo.File() - Render filesystem is ephemeral
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// ══════════════════════════════════════════════════
// STRONGLY-TYPED CONFIGURATION
// ══════════════════════════════════════════════════
builder.Services.Configure<SupabaseOptions>(
    builder.Configuration.GetSection(SupabaseOptions.Section));
builder.Services.Configure<HangfireOptions>(
    builder.Configuration.GetSection(HangfireOptions.Section));
builder.Services.Configure<DatabaseOptions>(
    builder.Configuration.GetSection(DatabaseOptions.Section));
builder.Services.Configure<CorsOptions>(
    builder.Configuration.GetSection(CorsOptions.Section));

// Read config values
var dbOptions = builder.Configuration
    .GetSection(DatabaseOptions.Section)
    .Get<DatabaseOptions>() ?? new DatabaseOptions();

var supabaseOptions = builder.Configuration
    .GetSection(SupabaseOptions.Section)
    .Get<SupabaseOptions>() ?? new SupabaseOptions();

var corsConfig = builder.Configuration
    .GetSection(CorsOptions.Section)
    .Get<CorsOptions>() ?? new CorsOptions();

// ══════════════════════════════════════════════════
// DATABASE — Supabase PgBouncer (Transaction Mode)
// Port 6543 = PgBouncer Transaction mode
// Do NOT use prepared statements — PgBouncer doesn't support them
// ══════════════════════════════════════════════════
var connectionString =
    Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Database connection string not configured");

var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);

// ⚠️ CRITICAL for PgBouncer Transaction mode:
// Prepared statements are NOT supported — must disable
dataSourceBuilder.ConnectionStringBuilder.NoResetOnClose = true;
dataSourceBuilder.ConnectionStringBuilder.MaxAutoPrepare = 0; // disable prepared statements
dataSourceBuilder.ConnectionStringBuilder.Pooling = false;    // let PgBouncer handle pooling
dataSourceBuilder.ConnectionStringBuilder.CommandTimeout = dbOptions.CommandTimeout;

var dataSource = dataSourceBuilder.Build();

builder.Services.AddDbContext<AppDbContext>(options =>
    options
        .UseNpgsql(dataSource, npgsql =>
        {
            npgsql.EnableRetryOnFailure(
                maxRetryCount: dbOptions.MaxRetryCount,
                maxRetryDelay: TimeSpan.FromSeconds(5),
                errorCodesToAdd: null
            );
            npgsql.CommandTimeout(dbOptions.CommandTimeout);
            // ⚠️ CRITICAL: disable for PgBouncer transaction mode
            npgsql.UseQuerySplittingBehavior(QuerySplittingBehavior.SplitQuery);
        })
        .EnableServiceProviderCaching()
);

// ══════════════════════════════════════════════════
// HANGFIRE — use session mode connection (port 5432)
// Hangfire needs persistent connections, NOT PgBouncer
// ══════════════════════════════════════════════════
var hangfireConnectionString =
    Environment.GetEnvironmentVariable("DATABASE_SESSION_URL")
    ?? connectionString; // fallback to same if no session URL

builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(options =>
        options.UseNpgsqlConnection(hangfireConnectionString),
        new PostgreSqlStorageOptions
        {
            QueuePollInterval = TimeSpan.FromSeconds(15),
            InvisibilityTimeout = TimeSpan.FromMinutes(30),
            DistributedLockTimeout = TimeSpan.FromSeconds(30),
            PrepareSchemaIfNecessary = true,
            EnableTransactionScopeEnlistment = true
        }));

builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = 2;
    options.Queues = new[] { "default" };
});

// ══════════════════════════════════════════════════
// REPOSITORIES & SERVICES
// ══════════════════════════════════════════════════
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserLoader, UserLoader>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IMealService, MealService>();
builder.Services.AddScoped<IMealRepository, MealRepository>();
builder.Services.AddScoped<IKitchenRepository, KitchenRepository>();
builder.Services.AddScoped<IKitchenService, KitchenService>();
builder.Services.AddScoped<IIngredientService, IngredientService>();
builder.Services.AddScoped<IIngredientRepository, IngredientRepository>();
builder.Services.AddScoped<IIngredientCategoryService, IngredientCategoryService>();
builder.Services.AddScoped<IIngredientCategoryRepository, IngredientCategoryRepository>();
builder.Services.AddScoped<IMealOptionService, MealOptionService>();
builder.Services.AddScoped<IMealOptionRepository, MealOptionRepository>();
builder.Services.AddScoped<IMealOptionIngredientService, MealOptionIngredientService>();
builder.Services.AddScoped<IMealOptionIngredientRepository, MealOptionIngredientRepository>();
builder.Services.AddScoped<IUserMealService, UserMealService>();
builder.Services.AddScoped<IUserMealRepository, UserMealRepository>();
builder.Services.AddScoped<IUserMealIngredientService, UserMealIngredientService>();
builder.Services.AddScoped<IUserMealIngredientRepository, UserMealIngredientRepository>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
builder.Services.AddScoped<IWalletTransactionService, WalletTransactionService>();
builder.Services.AddScoped<IWalletTransactionRepository, WalletTransactionRepository>();
builder.Services.AddScoped<IServiceableLocationRepository, ServiceableLocationRepository>();
builder.Services.AddScoped<IServiceableLocationService, ServiceableLocationService>();
builder.Services.AddScoped<IUserAddressRepository, UserAddressRepository>();
builder.Services.AddScoped<IUserAddressService, UserAddressService>();
builder.Services.AddScoped<IScheduledOrderRepository, ScheduledOrderRepository>();
builder.Services.AddScoped<IScheduledOrderService, ScheduledOrderService>();
builder.Services.AddScoped<ISubscriptionSchedulingService, SubscriptionSchedulingService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddHttpClient<ISupabaseStorageService, SupabaseStorageService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddMemoryCache();

// ══════════════════════════════════════════════════
// JSON + VALIDATION
// ══════════════════════════════════════════════════
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<
    Sovva.Application.Validators.CreateUserDtoValidator>();

// ══════════════════════════════════════════════════
// RESPONSE COMPRESSION
// ══════════════════════════════════════════════════
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<
        Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    options.Providers.Add<
        Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
    options.MimeTypes =
        Microsoft.AspNetCore.ResponseCompression.ResponseCompressionDefaults.MimeTypes
        .Concat(new[] { "application/json", "text/json" });
});

// ══════════════════════════════════════════════════
// CORS — environment-driven, supports all Vercel URLs
// ══════════════════════════════════════════════════
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .SetIsOriginAllowed(origin =>
            {
                if (string.IsNullOrEmpty(origin)) return false;
                Uri uri;
                if (!Uri.TryCreate(origin, UriKind.Absolute, out uri!)) return false;

                // Local development
                if (uri.Host == "localhost" || uri.Host == "127.0.0.1")
                    return true;

                // Vercel preview + production URLs for your project
                if (uri.Host.EndsWith(".vercel.app"))
                {
                    // Check against configured slugs OR your project slug
                    var slugs = corsConfig.AllowedVercelSlugs.Length > 0
                        ? corsConfig.AllowedVercelSlugs
                        : new[] { "rishijain21s-projects" };

                    if (slugs.Any(slug => uri.Host.Contains(slug)))
                        return true;
                }

                // Explicit production origins from config/env
                var productionOrigin = Environment.GetEnvironmentVariable("PRODUCTION_ORIGIN")
                    ?? builder.Configuration["Cors:ProductionOrigin"];

                if (!string.IsNullOrEmpty(productionOrigin) &&
                    origin.Equals(productionOrigin, StringComparison.OrdinalIgnoreCase))
                    return true;

                // Explicit allowed origins from config
                if (corsConfig.AllowedOrigins.Any(o =>
                    o.Equals(origin, StringComparison.OrdinalIgnoreCase)))
                    return true;

                return false;
            })
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// ══════════════════════════════════════════════════
// RATE LIMITING
// ══════════════════════════════════════════════════
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth", o =>
    {
        o.PermitLimit = 10;
        o.Window = TimeSpan.FromMinutes(1);
        o.QueueLimit = 0;
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    options.AddFixedWindowLimiter("default", o =>
    {
        o.PermitLimit = 100;
        o.Window = TimeSpan.FromMinutes(1);
        o.QueueLimit = 5;
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    options.RejectionStatusCode = 429;
    options.OnRejected = async (context, token) =>
    {
        var origin = context.HttpContext.Request.Headers["Origin"].ToString();
        if (!string.IsNullOrEmpty(origin))
            context.HttpContext.Response.Headers
                .Append("Access-Control-Allow-Origin", origin);

        context.HttpContext.Response.StatusCode = 429;
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { success = false, message = "Too many requests. Please try again later." },
            token);
    };
});

// ══════════════════════════════════════════════════
// AUTHENTICATION — Supabase JWKS
// ══════════════════════════════════════════════════
var supabaseUrl = (supabaseOptions.Url.Length > 0
    ? supabaseOptions.Url
    : "https://beeqamwptmbpowswawfx.supabase.co").TrimEnd('/');

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.Authority = $"{supabaseUrl}/auth/v1";
    options.MetadataAddress = $"{supabaseUrl}/auth/v1/.well-known/openid-configuration";
    options.RequireHttpsMetadata = true;
    options.SaveToken = true;
    options.IncludeErrorDetails = true;

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = $"{supabaseUrl}/auth/v1",
        ValidateAudience = true,
        ValidAudience = "authenticated",
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1),
        ValidateIssuerSigningKey = true,
        NameClaimType = "sub",
        RoleClaimType = "sovva_role"
    };

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            Log.Warning("JWT auth failed on {Path}: {Error}",
                context.Request.Path, context.Exception.Message);
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            var sub = context.Principal?.FindFirst("sub")?.Value;
            Log.Information("JWT validated for {Path}, user: {UserId}",
                context.Request.Path, sub);
            return Task.CompletedTask;
        },
        OnChallenge = context =>
        {
            Log.Warning("JWT challenge for {Path}: {Error}",
                context.Request.Path, context.Error ?? "unauthorized");
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization(options =>
{
    // ✅ Policy-based authorization using sovva_role claim from JWT
    options.AddPolicy("AdminOnly", policy =>
        policy.RequireClaim("sovva_role", "Admin"));

    options.AddPolicy("UserOnly", policy =>
        policy.RequireClaim("sovva_role", "User"));
});

// ══════════════════════════════════════════════════
// SWAGGER
// ══════════════════════════════════════════════════
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Sovva API", Version = "v1",
        Description = "Sovva Healthy Breakfast Platform API"
    });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {{
        new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference
                { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
        },
        new List<string>()
    }});
});

// ══════════════════════════════════════════════════
// HEALTH CHECKS — liveness vs readiness separation
// ══════════════════════════════════════════════════
// Health check — URI format (required by NpgSql health check library)
var healthCheckConnectionString =
    Environment.GetEnvironmentVariable("DATABASE_SESSION_URL_URI")
    ?? connectionString;

builder.Services.AddHealthChecks()
    .AddNpgSql(
        healthCheckConnectionString,
        name: "postgres",
        failureStatus: HealthStatus.Degraded, // degraded ≠ dead — won't kill Render instance
        tags: new[] { "db", "ready" },
        timeout: TimeSpan.FromSeconds(3)
    )
    .AddCheck("self",
        () => HealthCheckResult.Healthy("API is running"),
        tags: new[] { "live" });

// ══════════════════════════════════════════════════
// BUILD APP
// ══════════════════════════════════════════════════
var app = builder.Build();

// Middleware order matters — do not rearrange
app.UseMiddleware<GlobalExceptionMiddleware>(); // 1. catch all errors
app.UseMiddleware<CorrelationIdMiddleware>();     // 2. add correlation ID for tracing
app.UseCors("AllowFrontend");                  // 2. CORS headers on every response
app.UseSerilogRequestLogging();                // 3. request logs
app.UseResponseCompression();                  // 4. compress
app.UseRateLimiter();                          // 5. rate limit

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Sovva API v1");
    c.RoutePrefix = "swagger";
});

app.UseAuthentication();
app.UseMiddleware<AuthMiddleware>();
app.UseAuthorization();

// ══════════════════════════════════════════════════
// HANGFIRE DASHBOARD
// ══════════════════════════════════════════════════
var hangfireUser = builder.Configuration["HangfireDashboard:Username"]
    ?? throw new InvalidOperationException("Hangfire username not configured");
var hangfirePass = builder.Configuration["HangfireDashboard:Password"]
    ?? throw new InvalidOperationException("Hangfire password not configured");

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireBasicAuthFilter(hangfireUser, hangfirePass) },
    DashboardTitle = "Sovva Jobs"
});

app.MapControllers();

// ══════════════════════════════════════════════════
// ENDPOINTS
// ══════════════════════════════════════════════════
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

// ══════════════════════════════════════════════════
// HANGFIRE JOBS
// ══════════════════════════════════════════════════
var logger = app.Services.GetRequiredService<ILogger<Program>>();
try
{
    var jobs = app.Services.GetRequiredService<IRecurringJobManager>();
    var istZone = TimeZoneHelper.IST;

    jobs.AddOrUpdate<ISubscriptionSchedulingService>(
        "subscription-order-generation",
        s => s.GenerateScheduledOrdersFromSubscriptionsAsync(),
        "1 0 * * *",
        new RecurringJobOptions
        {
            TimeZone = istZone,
            MisfireHandling = MisfireHandlingMode.Ignorable
        });

    jobs.AddOrUpdate<IScheduledOrderService>(
        "midnight-order-confirmation",
        s => s.ConfirmAllScheduledOrdersAsync(),
        "59 23 * * *",
        new RecurringJobOptions
        {
            TimeZone = istZone,
            MisfireHandling = MisfireHandlingMode.Ignorable
        });

    jobs.AddOrUpdate<ISubscriptionService>(
        "sync-subscription-dates",
        s => s.UpdateNextScheduledDatesAsync(),
        "55 23 * * *",
        new RecurringJobOptions
        {
            TimeZone = istZone,
            MisfireHandling = MisfireHandlingMode.Ignorable
        });

    jobs.AddOrUpdate<ISubscriptionService>(
        "expire-subscriptions",
        s => s.ExpireSubscriptionsAsync(),
        "50 23 * * *",
        new RecurringJobOptions
        {
            TimeZone = istZone,
            MisfireHandling = MisfireHandlingMode.Ignorable
        });

    logger.LogInformation("✅ Hangfire jobs scheduled successfully");
}
catch (Exception ex)
{
    logger.LogError(ex, "❌ Failed to schedule Hangfire jobs");
}

logger.LogInformation("🚀 Sovva API started | Env: {Env}",
    app.Environment.EnvironmentName);

app.Run();

// ══════════════════════════════════════════════════
// HEALTH CHECK RESPONSE WRITER
// ══════════════════════════════════════════════════
static Task WriteHealthResponse(HttpContext context, HealthReport report)
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

// ══════════════════════════════════════════════════
// HANGFIRE AUTH FILTER
// ══════════════════════════════════════════════════
public class HangfireBasicAuthFilter : Hangfire.Dashboard.IDashboardAuthorizationFilter
{
    private readonly string _username;
    private readonly string _password;

    public HangfireBasicAuthFilter(string username, string password)
    {
        _username = username;
        _password = password;
    }

    public bool Authorize(Hangfire.Dashboard.DashboardContext context)
    {
        if (context is not Hangfire.Dashboard.AspNetCoreDashboardContext aspNetContext)
            return false;

        var httpContext = aspNetContext.HttpContext;
        var authHeader = httpContext.Request.Headers["Authorization"].ToString();

        if (!string.IsNullOrEmpty(authHeader) &&
            authHeader.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var credentials = Encoding.UTF8.GetString(
                    Convert.FromBase64String(authHeader[6..]));
                var parts = credentials.Split(':', 2);
                if (parts.Length == 2 &&
                    parts[0] == _username &&
                    parts[1] == _password)
                {
                    return true; // ✅ Credentials valid — show dashboard
                }
            }
            catch { }
        }

        // ✅ KEY FIX: Challenge the browser to show the login prompt
        httpContext.Response.StatusCode = 401;
        httpContext.Response.Headers["WWW-Authenticate"] = "Basic realm=\"Sovva Hangfire\"";
        return false;
    }
}