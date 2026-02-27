// HealthyBreakfastApp.WebAPI/Program.cs

using HealthyBreakfastApp.Application.Interfaces;
using HealthyBreakfastApp.Application.Services;
using HealthyBreakfastApp.Infrastructure.Data;
using HealthyBreakfastApp.Infrastructure.Repositories;
using HealthyBreakfastApp.WebAPI.Middleware;
using HealthyBreakfastApp.WebAPI.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Text.Json;
using Hangfire;
using Hangfire.PostgreSql;

var builder = WebApplication.CreateBuilder(args);

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

// ✅ ENHANCED: Logging configuration
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
    logging.SetMinimumLevel(LogLevel.Information);
});

// ========================================
// ⚙️ JSON SERIALIZATION SETTINGS
// ========================================
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

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
    options.IncludeErrorDetails = true;

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
        RoleClaimType = "role"
    };

    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine($"❌ JWT Authentication failed for {context.Request.Path}: {context.Exception.Message}");
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            var path = context.Request.Path;
            var sub = context.Principal?.FindFirst("sub")?.Value;
            Console.WriteLine($"✅ JWT Token validated for {path} - User: {sub}");
            return Task.CompletedTask;
        },
        OnChallenge = context =>
        {
            Console.WriteLine($"⚠️ JWT Challenge for {context.Request.Path}: {context.Error}");
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
// 🚀 BUILD AND CONFIGURE PIPELINE
// ========================================
var app = builder.Build();

app.UseResponseCompression(); // ✅ ADD THIS LINE

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowAngular");

// ========================================
// 🎛️ HANGFIRE DASHBOARD
// ========================================
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireDashboardNoAuthFilter() } // ⚠️ Development only!
});

app.UseAuthentication();
app.UseMiddleware<AuthMiddleware>();
app.UseAuthorization();

app.MapControllers();

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
       "59 23 * * *",  
        new RecurringJobOptions
        {
            TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata")
        });
    
    logger.LogInformation("✅ Hangfire job scheduled: Midnight order confirmation (12:00 AM IST)");
    
    // ✅ JOB 3: SUBSCRIPTION DATE SYNC (11:59 PM IST)
    // Updates NextScheduledDate for all active subscriptions
    // Keeps subscription cards showing accurate "Next Delivery" dates
    recurringJobManager.AddOrUpdate<ISubscriptionService>(
        "sync-subscription-dates",
        service => service.UpdateNextScheduledDatesAsync(),
        "59 23 * * *",  // Every day at 11:59 PM IST (1 minute before midnight)
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
// 🔓 HANGFIRE DASHBOARD AUTH FILTER (Development Only)
// ========================================
public class HangfireDashboardNoAuthFilter : Hangfire.Dashboard.IDashboardAuthorizationFilter
{
    public bool Authorize(Hangfire.Dashboard.DashboardContext context)
    {
        // ⚠️ DEVELOPMENT ONLY - Allow all access
        // TODO: Add proper authentication in production
        return true;
    }
}

