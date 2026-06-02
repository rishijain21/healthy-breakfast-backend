using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;
using Sovva.Application.Services;
using Sovva.Domain.Constants;
using Sovva.Infrastructure.Data;
using Sovva.Infrastructure.Repositories;
using Sovva.Infrastructure.Services;
using Sovva.WebAPI.Configuration;
using Sovva.WebAPI.Infrastructure;
using Sovva.WebAPI.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text.Json;
using System.Threading.RateLimiting;
using FluentValidation;
using FluentValidation.AspNetCore;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using Serilog;

namespace Sovva.WebAPI.Extensions;

/// <summary>
/// Extension methods that register all DI services for the Sovva application.
/// Extracted from Program.cs for maintainability (ARCH-03).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers strongly-typed configuration options from appsettings.
    /// </summary>
    public static IServiceCollection AddAppConfiguration(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SupabaseOptions>(
            configuration.GetSection(SupabaseOptions.Section));
        services.Configure<HangfireOptions>(
            configuration.GetSection(HangfireOptions.Section));
        services.Configure<DatabaseOptions>(
            configuration.GetSection(DatabaseOptions.Section));
        services.Configure<CorsOptions>(
            configuration.GetSection(CorsOptions.Section));

        return services;
    }

    /// <summary>
    /// Registers AppDbContext with Npgsql and EF Core interceptors.
    /// </summary>
    public static IServiceCollection AddDatabase(
        this IServiceCollection services, IConfiguration configuration)
    {
        var dbOptions = configuration
            .GetSection(DatabaseOptions.Section)
            .Get<DatabaseOptions>() ?? new DatabaseOptions();

        var connectionString =
            Environment.GetEnvironmentVariable("DATABASE_SESSION_URL")
            ?? Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Database connection string not configured");

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.ConnectionStringBuilder.CommandTimeout = dbOptions.CommandTimeout;
        var dataSource = dataSourceBuilder.Build();

        services.AddDbContext<AppDbContext>((sp, options) =>
            options
                .UseNpgsql(dataSource, npgsql =>
                {
                    npgsql.EnableRetryOnFailure(
                        maxRetryCount: dbOptions.MaxRetryCount,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorCodesToAdd: null
                    );
                    npgsql.CommandTimeout(dbOptions.CommandTimeout);
                })
                .EnableServiceProviderCaching()
                .AddInterceptors(sp.GetRequiredService<TimestampInterceptor>())
        );

        return services;
    }

    /// <summary>
    /// Registers Hangfire server and storage using PostgreSQL (session mode).
    /// </summary>
    public static IServiceCollection AddHangfireServices(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("DATABASE_SESSION_URL")
            ?? Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Database connection string not configured");

        var hangfireConnectionString =
            Environment.GetEnvironmentVariable("DATABASE_SESSION_URL")
            ?? connectionString;

        services.AddHangfire(config => config
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

        services.AddHangfireServer(options =>
        {
            options.WorkerCount = 2;
            options.Queues = new[] { "default" };
        });

        return services;
    }

    public static IServiceCollection AddAppCaching(this IServiceCollection services, IConfiguration configuration)
    {
        var redisConnectionString = configuration.GetConnectionString("RedisConnection");

        if (!string.IsNullOrWhiteSpace(redisConnectionString))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = redisConnectionString;
                options.InstanceName = "Sovva_";
            });
        }
        else
        {
            // Fallback to in-memory cache for local development without Redis
            services.AddDistributedMemoryCache();
        }

        return services;
    }

    /// <summary>
    /// Registers all Application and Infrastructure layer services (repositories, services, helpers).
    /// </summary>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Caching
        services.AddScoped<ICacheService, CacheService>();
        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IUserLoader, UserLoader>();
        services.AddScoped<IMealRepository, MealRepository>();
        services.AddScoped<IKitchenRepository, KitchenRepository>();
        services.AddScoped<IIngredientRepository, IngredientRepository>();
        services.AddScoped<IIngredientCategoryRepository, IngredientCategoryRepository>();
        services.AddScoped<IMealOptionRepository, MealOptionRepository>();
        services.AddScoped<IMealOptionIngredientRepository, MealOptionIngredientRepository>();
        services.AddScoped<IUserMealRepository, UserMealRepository>();
        services.AddScoped<IUserMealIngredientRepository, UserMealIngredientRepository>();
        services.AddScoped<IOrderRepository, OrderRepository>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
        services.AddScoped<IWalletTransactionRepository, WalletTransactionRepository>();
        services.AddScoped<IServiceableLocationRepository, ServiceableLocationRepository>();
        services.AddScoped<IUserAddressRepository, UserAddressRepository>();
        services.AddScoped<IScheduledOrderRepository, ScheduledOrderRepository>();

        // Application services
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IMealService, MealService>();
        services.AddScoped<IKitchenService, KitchenService>();
        services.AddScoped<IIngredientService, IngredientService>();
        services.AddScoped<IIngredientCategoryService, IngredientCategoryService>();
        services.AddScoped<IMealOptionService, MealOptionService>();
        services.AddScoped<IMealOptionIngredientService, MealOptionIngredientService>();
        services.AddScoped<IUserMealService, UserMealService>();
        services.AddScoped<IUserMealIngredientService, UserMealIngredientService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<IWalletTransactionService, WalletTransactionService>();
        services.AddScoped<IServiceableLocationService, ServiceableLocationService>();
        services.AddScoped<IUserAddressService, UserAddressService>();
        services.AddScoped<IScheduledOrderService, ScheduledOrderService>();
        services.AddScoped<ISubscriptionSchedulingService, SubscriptionSchedulingService>();
        services.AddScoped<IDashboardService, DashboardService>();

        // Infrastructure & helpers
        services.AddSingleton<IAppTimeProvider, AppTimeProvider>();
        services.AddSingleton<TimestampInterceptor>();
        services.AddHttpClient<ISupabaseStorageService, SupabaseStorageService>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddMemoryCache();
        services.AddSingleton<JobFailureAlertFilter>();

        return services;
    }

    /// <summary>
    /// Configures JSON serialization, FluentValidation, response compression, and file upload limits.
    /// </summary>
    public static IServiceCollection AddApiInfrastructure(this IServiceCollection services)
    {
        // File upload size limit (10MB for meal photos)
        services.Configure<Microsoft.AspNetCore.Http.Features.FormOptions>(options =>
        {
            options.MultipartBodyLengthLimit = 10 * 1024 * 1024; // 10MB
        });

        // JSON + validation
        services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
                options.JsonSerializerOptions.DefaultIgnoreCondition =
                    System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
                options.JsonSerializerOptions.Converters.Add(new DateOnlyJsonConverter());
                options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
            });

        services.AddFluentValidationAutoValidation();
        services.AddValidatorsFromAssemblyContaining<
            Sovva.Application.Validators.CreateUserDtoValidator>();

        // Response compression
        services.AddResponseCompression(options =>
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

        return services;
    }

    /// <summary>
    /// Configures CORS with explicit allowlist from configuration.
    /// </summary>
    public static IServiceCollection AddAppCors(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("CorsPolicy", policy =>
            {
                var allowedOrigins = configuration
                    .GetSection("Cors:AllowedOrigins")
                    .Get<string[]>() ?? Array.Empty<string>();

                policy
                    .WithOrigins(allowedOrigins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

        return services;
    }

    /// <summary>
    /// Configures fixed-window rate limiting policies.
    /// </summary>
    public static IServiceCollection AddAppRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
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

            options.AddFixedWindowLimiter("financial", o =>
            {
                o.PermitLimit = 15;
                o.Window = TimeSpan.FromMinutes(1);
                o.QueueLimit = 2;
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

        return services;
    }

    /// <summary>
    /// Configures Supabase JWT authentication and authorization policies.
    /// </summary>
    public static IServiceCollection AddAppAuth(
        this IServiceCollection services, IConfiguration configuration)
    {
        var supabaseOptions = configuration
            .GetSection(SupabaseOptions.Section)
            .Get<SupabaseOptions>() ?? new SupabaseOptions();

        var supabaseUrl = (supabaseOptions.Url.Length > 0
            ? supabaseOptions.Url
            : "https://beeqamwptmbpowswawfx.supabase.co").TrimEnd('/');

        services.AddAuthentication(options =>
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
                ClockSkew = TimeSpan.FromMinutes(5),
                ValidateIssuerSigningKey = true,
                NameClaimType = "sub",
                RoleClaimType = RoleConstants.SovvaRole
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

        services.AddAuthorization(options =>
        {
            options.AddPolicy("AdminOnly", policy =>
                policy.RequireClaim(RoleConstants.SovvaRole, RoleConstants.Admin));

            options.AddPolicy("UserOnly", policy =>
                policy.RequireClaim(RoleConstants.SovvaRole, RoleConstants.Customer));
        });

        return services;
    }

    /// <summary>
    /// Configures Swagger/OpenAPI (development only).
    /// </summary>
    public static IServiceCollection AddAppSwagger(this IServiceCollection services)
    {
        services.AddSwaggerGen(c =>
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

        return services;
    }

    /// <summary>
    /// Registers health checks for database, Hangfire, and self-check.
    /// </summary>
    public static IServiceCollection AddAppHealthChecks(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("DATABASE_SESSION_URL")
            ?? Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Database connection string not configured");

        var healthCheckConnectionString =
            Environment.GetEnvironmentVariable("DATABASE_SESSION_URL")
            ?? connectionString;

        services.AddHealthChecks()
            .AddNpgSql(
                healthCheckConnectionString,
                name: "database",
                failureStatus: HealthStatus.Degraded,
                tags: new[] { "db", "ready" },
                timeout: TimeSpan.FromSeconds(3)
            )
            .AddHangfire(options => {
                options.MinimumAvailableServers = 1;
            }, name: "hangfire")
            .AddCheck("self",
                () => HealthCheckResult.Healthy("API is running"),
                tags: new[] { "live" });

        return services;
    }
}
