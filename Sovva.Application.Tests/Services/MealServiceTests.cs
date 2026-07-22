using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Sovva.Application.Common.Infrastructure;
using Sovva.Application.DTOs;
using Sovva.Application.Features.Catalog.Commands.CreateMeal;
using Sovva.Application.Features.Catalog.Commands.CreateMealWithOptions;
using Sovva.Application.Features.Catalog.Commands.UpdateMeal;
using Sovva.Application.Features.Catalog.Queries.CalculateMealPrice;
using Sovva.Application.Features.Catalog.Queries.GetActiveMeals;
using Sovva.Application.Features.Catalog.Queries.GetIngredientBreakdown;
using Sovva.Application.Features.Catalog.Queries.GetIngredientsTotalPrice;
using Sovva.Application.Features.Catalog.Queries.GetMealById;
using Sovva.Application.Features.Catalog.Queries.GetMealDetailForAdmin;
using Sovva.Application.Features.Catalog.Queries.GetNutritionalSummary;
using Sovva.Application.Features.Catalog.Queries.ValidateIngredientSelection;
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;
using Sovva.Application.Services;
using Sovva.Application.Tests.Helpers;
using Sovva.Domain.Entities;
using Xunit;

namespace Sovva.Application.Tests.Services;

public class MealServiceTests
{
    private readonly Mock<IMealRepository> _mealRepoMock = new();
    private readonly Mock<IIngredientRepository> _ingredientRepoMock = new();
    private readonly Mock<IMealOptionRepository> _mealOptionRepoMock = new();
    private readonly Mock<IIngredientCategoryRepository> _categoryRepoMock = new();
    private readonly Mock<IMealOptionIngredientRepository> _moiRepoMock = new();
    private readonly Mock<ISupabaseStorageService> _storageMock = new();
    private readonly Mock<ICacheService> _cacheMock = new();
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IAppTimeProvider> _timeMock = new();
    private readonly TestMediatRSender _sender = new();
    private readonly IMealService _mealService;

    public MealServiceTests()
    {
        _timeMock.Setup(t => t.UtcNow).Returns(new DateTime(2026, 7, 12, 10, 0, 0, DateTimeKind.Utc));

        // Register Handlers
        _sender.Register(new CreateMealCommandHandler(_mealRepoMock.Object, _uowMock.Object, _cacheMock.Object, _timeMock.Object));
        _sender.Register(new GetActiveMealsQueryHandler(_mealRepoMock.Object, _cacheMock.Object, _storageMock.Object, Mock.Of<ILogger<GetActiveMealsQueryHandler>>()));
        _sender.Register(new GetMealByIdQueryHandler(_mealRepoMock.Object, _cacheMock.Object, _storageMock.Object, Mock.Of<ILogger<GetMealByIdQueryHandler>>()));
        _sender.Register(new GetIngredientsTotalPriceQueryHandler());
        _sender.Register(new GetNutritionalSummaryQueryHandler());
        _sender.Register(new ValidateIngredientSelectionQueryHandler(_mealOptionRepoMock.Object));
        _sender.Register(new GetIngredientBreakdownQueryHandler());
        _sender.Register(new CalculateMealPriceQueryHandler(_mealRepoMock.Object, _ingredientRepoMock.Object, _sender));
        _sender.Register(new GetMealDetailForAdminQueryHandler(_mealRepoMock.Object, _storageMock.Object, Mock.Of<ILogger<GetMealDetailForAdminQueryHandler>>()));

        _mealService = new MealService(_sender);
    }

    [Fact]
    public async Task CreateMealAsync_ValidDto_SavesMealAndInvalidatesCache()
    {
        // Arrange
        var dto = new CreateMealDto
        {
            MealName = "Oatmeal Power Bowl",
            Description = "Healthy oats with berries",
            BasePrice = 120m,
            ApproxCalories = 350
        };

        _mealRepoMock.Setup(r => r.AddMealAsync(It.IsAny<Meal>()))
            .Callback<Meal>(m => m.MealId = 101)
            .Returns(Task.CompletedTask);

        // Act
        var mealId = await _mealService.CreateMealAsync(dto);

        // Assert
        Assert.Equal(101, mealId);
        _mealRepoMock.Verify(r => r.AddMealAsync(It.Is<Meal>(m => m.MealName == "Oatmeal Power Bowl" && m.BasePrice == 120m)), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
        _cacheMock.Verify(c => c.RemoveAsync(It.Is<string>(k => k.Contains("meals:active"))), Times.AtLeastOnce);
    }

    [Fact]
    public async Task GetActiveMealsAsync_ClampsPageSizeAndReturnsCachedOrFresh()
    {
        // Arrange
        _cacheMock.Setup(c => c.GetAsync<PagedResult<MealDto>>(It.IsAny<string>()))
            .ReturnsAsync((PagedResult<MealDto>?)null);

        var activeMeals = new List<Meal>
        {
            new Meal { MealId = 1, MealName = "Breakfast Burrito", BasePrice = 150m, IsComplete = true, ImageUrl = "/meal-images/meal-1/burrito.jpg" },
            new Meal { MealId = 2, MealName = "Incomplete Meal", BasePrice = 100m, IsComplete = false }
        };

        _mealRepoMock.Setup(r => r.GetActiveMealsAsync(1, 50))
            .ReturnsAsync((activeMeals, 2));

        _storageMock.Setup(s => s.GetSignedUrlAsync("meal-1/burrito.jpg", 3600))
            .ReturnsAsync("https://signed-url/meal-1/burrito.jpg");

        // Act
        var result = await _mealService.GetActiveMealsAsync(0, 500); // Should clamp to page 1, pageSize 50

        // Assert
        Assert.Equal(1, result.Page);
        Assert.Equal(50, result.PageSize);
        Assert.Single(result.Items); // Only IsComplete == true should be included
        Assert.Equal("Breakfast Burrito", result.Items.First().MealName);
        Assert.Equal("https://signed-url/meal-1/burrito.jpg", result.Items.First().ImageUrl);
        _cacheMock.Verify(c => c.GetAsync<PagedResult<MealDto>>(It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task CalculateMealPriceAsync_ValidIngredients_ComputesAccurateBreakdown()
    {
        // Arrange
        var meal = new Meal { MealId = 1, MealName = "Avocado Toast", BasePrice = 90m };
        _mealRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(meal);

        var ingredients = new Dictionary<int, Ingredient>
        {
            { 10, new Ingredient { IngredientId = 10, CategoryId = 5, IngredientName = "Extra Avocado", Price = 40m, Calories = 120, Protein = 2m, Fiber = 5m, IsAvailable = true } },
            { 11, new Ingredient { IngredientId = 11, CategoryId = 5, IngredientName = "Chia Seeds", Price = 20m, Calories = 60, Protein = 3m, Fiber = 4m, IsAvailable = true } }
        };

        _ingredientRepoMock.Setup(r => r.GetByIdsAsync(It.IsAny<IEnumerable<int>>()))
            .ReturnsAsync(ingredients);

        _mealOptionRepoMock.Setup(r => r.GetByMealIdAsync(1))
            .ReturnsAsync(new List<MealOption>
            {
                new MealOption { MealOptionId = 1, MealId = 1, CategoryId = 5, IsRequired = false, MaxSelectable = 3 }
            });

        var calcDto = new MealPriceCalculationDto
        {
            MealId = 1,
            SelectedIngredients = new List<SelectedIngredientDto>
            {
                new SelectedIngredientDto { IngredientId = 10, Quantity = 1 },
                new SelectedIngredientDto { IngredientId = 11, Quantity = 2 } // 2 * 20m = 40m
            }
        };

        // Act
        var response = await _mealService.CalculateMealPriceAsync(calcDto);

        // Assert
        Assert.Equal(1, response.MealId);
        Assert.Equal(90m, response.BaseMealPrice);
        Assert.Equal(80m, response.IngredientsPrice); // 40 + (2 * 20) = 80
        Assert.Equal(170m, response.TotalPrice); // 90 + 80 = 170
        Assert.Equal(240, response.TotalCalories); // 120 + (2 * 60) = 240
        Assert.Equal(8m, response.TotalProtein); // 2 + (2 * 3) = 8
        Assert.Equal(13m, response.TotalFiber); // 5 + (2 * 4) = 13
        Assert.Equal(2, response.IngredientBreakdown.Count);
    }

    [Fact]
    public void CreateMealCommandValidator_RejectsNegativePriceOrEmptyName()
    {
        var validator = new CreateMealCommandValidator();

        var invalidDto = new CreateMealDto { MealName = "", BasePrice = -10m };
        var validationResult = validator.Validate(new CreateMealCommand(invalidDto));

        Assert.False(validationResult.IsValid);
        Assert.Contains(validationResult.Errors, e => e.PropertyName.Contains("MealName"));
        Assert.Contains(validationResult.Errors, e => e.PropertyName.Contains("BasePrice"));
    }
}
