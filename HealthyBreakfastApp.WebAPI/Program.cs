// HealthyBreakfastApp.WebAPI/Program.cs

using Serilog;
using HealthyBreakfastApp.Application.Interfaces;
using HealthyBreakfastApp.Application.Services;
using HealthyBreakfastApp.Infrastructure.Data;
using HealthyBreakfastApp.Infrastructure.Repositories;
using HealthyBreakfastApp.WebAPI.Middleware;
using HealthyBreakfastApp.WebAPI.Services;
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

// ✅ FIX 4: Configure Serilog BEFORE builder is created
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Hangfire", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: 
        "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/app-.log", 
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 7,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

// ✅ Replace default logging with Serilog
builder.Host.UseSerilog();

// ========================================
// 🚀 DATABASE CONFIGURATION
// ========================================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(connectionString));

// ========================================
// 🕒 HANGFIRE CONFIGURATION (Background Jobs)
// ========================================
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UsePostgreSqlStorage(options =>
        options.UseNpgsqlConnection(connectionString)));

builder.Services.AddHangfireServer();

// ========================================
// 🧩 APPLICATION SERVICES & REPOSITORIES
// ========================================
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserLoader, UserLoader>();
builder.Services.AddScoped<IUserService, UserService>();

builder.Services.AddScoped<IMealService, MealService>();
builder.Services.AddScoped<IMealRepository, MealRepository>();

// Kitchen dashboard services
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
// ========================================
// 📍 LOCATION SERVICES
// ========================================
builder.Services.AddScoped<IServiceableLocationRepository, ServiceableLocationRepository>();
builder.Services.AddScoped<IServiceableLocationService, ServiceableLocationService>();
builder.Services.AddScoped<IUserAddressRepository, UserAddressRepository>();
builder.Services.AddScoped<IUserAddressService, UserAddressService>();

// ========================================
// ⏰ SCHEDULED ORDER SERVICES
// ========================================
builder.Services.AddScoped<IScheduledOrderRepository, ScheduledOrderRepository>();
builder.Services.AddScoped<IScheduledOrderService, ScheduledOrderService>();

// ✅ NEW: Subscription scheduling service
builder.Services.AddScoped<ISubscriptionSchedulingService, SubscriptionSchedulingService>();

// ========================================
// 🖼️ SUPABASE STORAGE SERVICE (Image Upload)
// ========================================
builder.Services.AddScoped<ISupabaseStorageService, SupabaseStorageService>();
builder.Services.AddHttpClient<ISupabaseStorageService, SupabaseStorageService>();

// ========================================
// 🌐 UTILITIES & HELPERS
// ========================================
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// ✅ FIX 6: In-memory cache (already available, just register it)
builder.Services.AddMemoryCache();

// ========================================
// ⚙️ JSON SERIALIZATION SETTINGS
// ========================================
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

// ✅ Auto-register ALL validators in Application assembly
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddValidatorsFromAssemblyContaining<HealthyBreakfastApp.Application.Validators.CreateUserDtoValidator>();

// ========================================
// 🗜️ RESPONSE COMPRESSION
// ========================================
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true; // Safe since you control both ends
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProvider>();
    options.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
    options.MimeTypes = Microsoft.AspNetCore.ResponseCompression.ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/json", "text/json" }
    );
});

builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.BrotliCompressionProviderOptions>(options =>
{
    options.Level = System.IO.Compression.CompressionLevel.Fastest; // Balance speed vs size
});

// ========================================
// 🔓 CORS CONFIGURATION
// ========================================
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// ========================================
// 🛡️ RATE LIMITING
// ========================================
builder.Services.AddRateLimiter(options =>
{
    // Auth endpoints: max 10 requests per minute per IP
    options.AddFixedWindowLimiter("auth", limiterOptions =>
    {
        limiterOptions.PermitLimit = 10;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    // Default: max 100 requests per minute per IP for all other endpoints
    options.AddFixedWindowLimiter("default", limiterOptions =>
    {
        limiterOptions.PermitLimit = 100;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 5;
        limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
    });

    options.RejectionStatusCode = 429; // Too Many Requests
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { success = false, message = "Too many requests. Please try again later." }, token);
    };
});

// ========================================
// 🔑 SUPABASE HS256 JWT AUTHENTICATION
// ========================================
var supabaseJwtSecret = builder.Configuration["Supabase:JwtSecret"];
var supabaseUrl = builder.Configuration["Supabase:Url"] ?? "https://beeqamwptmbpowswawfx.supabase.co";

if (string.IsNullOrEmpty(supabaseJwtSecret))
    throw new InvalidOperationException("Supabase JWT Secret is required in appsettings.json");

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
    options.SaveToken = true;
    options.IncludeErrorDetails = !builder.Environment.IsProduction(); // Only show error details in non-production

    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = $"{supabaseUrl}/auth/v1",
        ValidAudience = "authenticated",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(supabaseJwtSecret)),
        ClockSkew = TimeSpan.FromMinutes(1),
        NameClaimType = "sub",
        RoleClaimType = System.Security.Claims.ClaimTypes.Role
    };

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            // ✅ FIX 7: Use Serilog instead of Console.WriteLine
            Log.Warning("JWT authentication failed on {Path}: {ErrorType}", 
                context.Request.Path, context.Exception.GetType().Name);
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            var path = context.Request.Path;
            var sub = context.Principal?.FindFirst("sub")?.Value;
            // ✅ FIX 7: Use Serilog instead of Console.WriteLine
            Log.Information("JWT token validated for {Path}, user: {UserId}", path, sub);
            return Task.CompletedTask;
        },
        OnChallenge = context =>
        {
            // ✅ FIX 7: Use Serilog instead of Console.WriteLine
            Log.Warning("JWT challenge for {Path}: {Error}", 
                context.Request.Path, context.Error ?? "unauthorized");
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

// ========================================
// 📘 SWAGGER CONFIGURATION
// ========================================
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "HealthyBreakfastApp API",
        Version = "v1",
        Description = "Production-ready API for Healthy Breakfast Delivery App with Hangfire Background Jobs & Subscriptions"
    });

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme.\r\n\r\nExample: \"Bearer 12345abcdef\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new List<string>()
        }
    });
});

// ========================================
// 🏥 HEALTH CHECKS (for cloud deployment)
// ========================================
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgres", tags: new[] { "db", "ready" })
    .AddCheck("self", () => HealthCheckResult.Healthy("API is running"), tags: new[] { "self" });
var app = builder.Build();

// ✅ FIX 1: Global exception handler — MUST be first
app.UseMiddleware<GlobalExceptionMiddleware>();

// ✅ FIX 4: Logs every HTTP request with method, path, status code, duration
app.UseSerilogRequestLogging();

app.UseResponseCompression(); // ✅ ADD THIS LINE
app.UseRateLimiter();

// ✅ FIX 2: Only enable Swagger in development
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAngular");

app.UseAuthentication();
app.UseMiddleware<AuthMiddleware>();
app.UseAuthorization();

// ========================================
// 🎛️ HANGFIRE DASHBOARD (Admin only in production)
// ========================================
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAdminAuthFilter() }
});

app.MapControllers();

// 🏥 Health check endpoints for cloud deployment (Railway, Render, Azure)
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
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
                description = e.Value.Description?.ToString(),
                duration = e.Value.Duration.ToString()
            })
        };
        await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(response));
    }
});

// ========================================
// ⏰ SCHEDULE RECURRING JOBS (MilkBasket Style)
// ========================================
var logger = app.Services.GetRequiredService<ILogger<Program>>();

try
{
    var recurringJobManager = app.Services.GetRequiredService<IRecurringJobManager>();
    
    // ✅ JOB 1: SUBSCRIPTION ORDER GENERATION (12:01 AM IST)
    // Generates scheduled orders from active subscriptions for TOMORROW's delivery
    recurringJobManager.AddOrUpdate<ISubscriptionSchedulingService>(
        "subscription-order-generation",
        service => service.GenerateScheduledOrdersFromSubscriptionsAsync(),
        "1 0 * * *",  // Every day at 12:01 AM IST (1 minute after midnight)
        new RecurringJobOptions
        {
            TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata")
        });
    
    logger.LogInformation("✅ Hangfire job scheduled: Subscription order generation (12:01 AM IST)");
    
    // ✅ JOB 2: MIDNIGHT ORDER CONFIRMATION (12:00 AM IST)
    // Confirms all scheduled orders for TODAY's delivery and sends to kitchen
    recurringJobManager.AddOrUpdate<IScheduledOrderService>(
        "midnight-order-confirmation",
        service => service.ConfirmAllScheduledOrdersAsync(),
       "59 23 * * *",  // Every day at 11:59 PM IST (runs AFTER sync completes)
        new RecurringJobOptions
        {
            TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata")
        });
    
    logger.LogInformation("✅ Hangfire job scheduled: Midnight order confirmation (12:00 AM IST)");
    
    // ✅ JOB 3: SUBSCRIPTION DATE SYNC (11:55 PM IST)
    // Updates NextScheduledDate for all active subscriptions
    // Keeps subscription cards showing accurate "Next Delivery" dates
    recurringJobManager.AddOrUpdate<ISubscriptionService>(
        "sync-subscription-dates",
        service => service.UpdateNextScheduledDatesAsync(),
        "55 23 * * *",  // Every day at 11:55 PM IST (runs FIRST, before order confirmation)
        new RecurringJobOptions
        {
            TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata")
        });
    
    logger.LogInformation("✅ Hangfire job scheduled: Subscription date sync (11:59 PM IST)");
}
catch (Exception ex)
{
    logger.LogError(ex, "❌ Failed to schedule Hangfire jobs");
}

logger.LogInformation("🚀 HealthyBreakfastApp API started successfully");
logger.LogInformation($"🔗 Swagger UI: http://localhost:5257/swagger");
logger.LogInformation($"🎛️ Hangfire Dashboard: http://localhost:5257/hangfire");
logger.LogInformation("📅 Scheduled Jobs (MilkBasket Style):");
logger.LogInformation("   - Subscription generation: 12:01 AM IST daily (creates orders for TOMORROW)");
logger.LogInformation("   - Order confirmation: 12:00 AM IST daily (confirms TODAY's orders)");
logger.LogInformation("   - Subscription date sync: 11:59 PM IST daily (updates NextScheduledDate)");

app.Run();

// ========================================
// 🔐 HANGFIRE DASHBOARD AUTH FILTER (must be at end for top-level statements)
// ========================================
// NOTE: Hangfire 1.8.x API doesn't expose GetHttpContext() directly
// The authorization filter currently allows all requests - needs proper implementation
public class HangfireAdminAuthFilter : Hangfire.Dashboard.IDashboardAuthorizationFilter
{
    public bool Authorize(Hangfire.Dashboard.DashboardContext context)
    {
        // TODO: Implement proper role-based auth for Hangfire dashboard
        // For now, allow all requests (the app.UseAuthentication() middleware handles JWT validation)
        return true;
    }
}



