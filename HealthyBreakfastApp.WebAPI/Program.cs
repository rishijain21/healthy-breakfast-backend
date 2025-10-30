using HealthyBreakfastApp.Application.Interfaces;
using HealthyBreakfastApp.Application.Services;
using HealthyBreakfastApp.Infrastructure.Data;
using HealthyBreakfastApp.Infrastructure.Repositories;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// ===== Add DbContext =====
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ===== Register Application Services =====
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IMealService, MealService>();
builder.Services.AddScoped<IMealRepository, MealRepository>();
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
builder.Services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IWalletTransactionRepository, WalletTransactionRepository>();
builder.Services.AddScoped<IWalletTransactionService, WalletTransactionService>();

// ===== ✅ ADD THESE TWO LINES =====
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// ===== Configure JSON Serialization =====
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

// ===== Add CORS Policy =====
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// ===== SUPABASE HS256 JWT Authentication =====
var supabaseJwtSecret = builder.Configuration["Supabase:JwtSecret"];
var supabaseUrl = builder.Configuration["Supabase:Url"] ?? "https://beeqamwptmbpowswawfx.supabase.co";

if (string.IsNullOrEmpty(supabaseJwtSecret))
{
    throw new InvalidOperationException("Supabase JWT Secret is required in appsettings.json");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
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
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($"❌ Authentication failed: {context.Exception.Message}");
                if (context.Exception.InnerException != null)
                    Console.WriteLine($"❌ Inner exception: {context.Exception.InnerException.Message}");
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                Console.WriteLine("✅ Token validated successfully (HS256)");
                var claims = context.Principal?.Claims?.Select(c => $"{c.Type}: {c.Value}") ?? new List<string>();
                Console.WriteLine($"Claims: {string.Join(", ", claims)}");
                return Task.CompletedTask;
            }
        };
    });
// In your Program.cs, add these lines:
builder.Services.AddScoped<IUserMealService, UserMealService>();
builder.Services.AddScoped<IUserMealIngredientService, UserMealIngredientService>();

// ===== Add Swagger with JWT support =====
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "HealthyBreakfastApp API", Version = "v1" });

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

var app = builder.Build();

// ===== Middleware =====
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("AllowAngular");

// ✅ ADD THIS CRITICAL LINE
app.UseMiddleware<HealthyBreakfastApp.WebAPI.Middleware.AuthMiddleware>();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();
