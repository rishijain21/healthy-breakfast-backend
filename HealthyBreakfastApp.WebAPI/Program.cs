using HealthyBreakfastApp.Application.Interfaces;
using HealthyBreakfastApp.Application.Services;
using HealthyBreakfastApp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using HealthyBreakfastApp.Infrastructure.Repositories;


var builder = WebApplication.CreateBuilder(args);

// Add DbContext
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


// ✅ Add CORS Policy
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:4200") // Angular app
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});



builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));



// Add controllers and swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("AllowFrontend");

app.UseAuthorization();

app.MapControllers();

app.Run();
