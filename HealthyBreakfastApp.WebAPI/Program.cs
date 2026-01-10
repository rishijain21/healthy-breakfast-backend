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
// ⏰ SCHEDULED ORDER SERVICES
// ========================================
builder.Services.AddScoped<IScheduledOrderRepository, ScheduledOrderRepository>();
builder.Services.AddScoped<IScheduledOrderService, ScheduledOrderService>();

// ⚠️ REMOVE OLD BACKGROUND SERVICE (Hangfire replaces it)
// builder.Services.AddHostedService<OrderConfirmationService>();

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
        Description = "Production-ready API for Healthy Breakfast Delivery App with Hangfire Background Jobs"
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
app.UseAuthorization();
app.UseMiddleware<AuthMiddleware>();

app.MapControllers();

// ========================================
// ⏰ SCHEDULE RECURRING JOBS (MilkBasket Style)
// ========================================
var logger = app.Services.GetRequiredService<ILogger<Program>>();

try
{
    var recurringJobManager = app.Services.GetRequiredService<IRecurringJobManager>();
    
    // ✅ MIDNIGHT ORDER CONFIRMATION JOB (12:00 AM IST)
    recurringJobManager.AddOrUpdate<IScheduledOrderService>(
        "midnight-order-confirmation",
        service => service.ConfirmAllScheduledOrdersAsync(),
        "0 0 * * *",  // Every day at 12:00 AM
        new RecurringJobOptions
        {
            TimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata")
        });
    
    logger.LogInformation("✅ Hangfire job scheduled: Midnight order confirmation (12:00 AM IST)");
}
catch (Exception ex)
{
    logger.LogError(ex, "❌ Failed to schedule Hangfire jobs");
}

logger.LogInformation("🚀 HealthyBreakfastApp API started successfully");
logger.LogInformation($"🔗 Swagger UI: http://localhost:5257/swagger");
logger.LogInformation($"🎛️ Hangfire Dashboard: http://localhost:5257/hangfire");

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
