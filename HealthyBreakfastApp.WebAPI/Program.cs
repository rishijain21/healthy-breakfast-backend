using HealthyBreakfastApp.Application.Interfaces;
using HealthyBreakfastApp.Application.Services;
using HealthyBreakfastApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using HealthyBreakfastApp.Infrastructure.Repositories;
using HealthyBreakfastApp.WebAPI.Middleware; // ADD this line
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);

// Add DbContext (ONLY ONCE)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register Application Services
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

builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

// ✅ Configure JSON serialization to use camelCase (THIS FIXES THE ISSUE!)
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    });

// ✅ Add CORS Policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200") // Angular app
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Add swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseSwagger();
app.UseSwaggerUI();

// ✅ Use CORS
app.UseCors("AllowAngular");

// ✅ ADD AUTH MIDDLEWARE (before authorization)
app.UseMiddleware<AuthMiddleware>();

app.UseAuthorization();
app.MapControllers();

app.Run();
