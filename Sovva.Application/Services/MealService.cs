using System.Collections.Generic;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.DTOs;
using Sovva.Application.Features.Catalog.Commands.CreateMeal;
using Sovva.Application.Features.Catalog.Commands.CreateMealWithOptions;
using Sovva.Application.Features.Catalog.Commands.DeleteMeal;
using Sovva.Application.Features.Catalog.Commands.DeleteMealImage;
using Sovva.Application.Features.Catalog.Commands.UpdateMeal;
using Sovva.Application.Features.Catalog.Commands.UpdateMealImage;
using Sovva.Application.Features.Catalog.Commands.UpdateMealStatus;
using Sovva.Application.Features.Catalog.Queries.CalculateMealPrice;
using Sovva.Application.Features.Catalog.Queries.GetActiveMeals;
using Sovva.Application.Features.Catalog.Queries.GetAllMealsForAdmin;
using Sovva.Application.Features.Catalog.Queries.GetAllMealsForAdminPaged;
using Sovva.Application.Features.Catalog.Queries.GetCategoriesWithIngredients;
using Sovva.Application.Features.Catalog.Queries.GetIngredientsTotalPrice;
using Sovva.Application.Features.Catalog.Queries.GetMealById;
using Sovva.Application.Features.Catalog.Queries.GetMealDetailForAdmin;
using Sovva.Application.Features.Catalog.Queries.GetMealsBatchDetails;
using Sovva.Application.Features.Catalog.Queries.GetMealsBatchDetailsForUsers;
using Sovva.Application.Features.Catalog.Queries.GetNutritionalSummary;
using Sovva.Application.Features.Catalog.Queries.ValidateIngredientSelection;
using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;

namespace Sovva.Application.Services;

/// <summary>
/// CQRS Facade for Catalog/Meal Operations. Delegates 100% of calls to MediatR handlers
/// under Sovva.Application/Features/Catalog/ while maintaining exact IMealService compatibility.
/// </summary>
public class MealService : IMealService
{
    private readonly ISender _sender;

    public MealService(ISender sender)
    {
        _sender = sender;
    }

    public Task<PagedResult<MealDto>> GetActiveMealsAsync(int page, int pageSize)
        => _sender.Send(new GetActiveMealsQuery(page, pageSize));

    public Task<int> CreateMealAsync(CreateMealDto dto)
        => _sender.Send(new CreateMealCommand(dto));

    public Task<MealDto?> GetMealByIdAsync(int id)
        => _sender.Send(new GetMealByIdQuery(id));

    public Task<MealPriceResponseDto> CalculateMealPriceAsync(MealPriceCalculationDto calculationDto)
        => _sender.Send(new CalculateMealPriceQuery(calculationDto));

    public Task<decimal> GetIngredientsTotalPriceAsync(List<SelectedIngredientDto> ingredients, IDictionary<int, Ingredient> ingredientMap)
        => _sender.Send(new GetIngredientsTotalPriceQuery(ingredients, ingredientMap));

    public Task<(int calories, decimal protein, decimal fiber)> GetNutritionalSummaryAsync(List<SelectedIngredientDto> ingredients, IDictionary<int, Ingredient> ingredientMap)
        => _sender.Send(new GetNutritionalSummaryQuery(ingredients, ingredientMap));

    public Task<bool> ValidateIngredientSelectionAsync(int mealId, List<SelectedIngredientDto> ingredients, IDictionary<int, Ingredient> ingredientMap)
        => _sender.Send(new ValidateIngredientSelectionQuery(mealId, ingredients, ingredientMap));

    public Task<List<AdminMealListDto>> GetAllMealsForAdminAsync()
        => _sender.Send(new GetAllMealsForAdminQuery());

    public Task<AdminMealDetailDto?> GetMealDetailForAdminAsync(int id)
        => _sender.Send(new GetMealDetailForAdminQuery(id));

    public Task<List<AdminMealDetailDto>> GetMealsBatchDetailsAsync(List<int> mealIds)
        => _sender.Send(new GetMealsBatchDetailsQuery(mealIds));

    public Task<int> CreateMealWithOptionsAsync(AdminCreateMealDto dto)
        => _sender.Send(new CreateMealWithOptionsCommand(dto));

    public Task<bool> UpdateMealAsync(int id, UpdateMealDto dto)
        => _sender.Send(new UpdateMealCommand(id, dto));

    public Task<bool> UpdateMealStatusAsync(int id, bool isComplete)
        => _sender.Send(new UpdateMealStatusCommand(id, isComplete));

    public Task<bool> DeleteMealAsync(int id)
        => _sender.Send(new DeleteMealCommand(id));

    public Task<List<CategoryWithIngredientsDto>> GetCategoriesWithIngredientsAsync()
        => _sender.Send(new GetCategoriesWithIngredientsQuery());

    public Task<PagedResult<AdminMealListDto>> GetAllMealsForAdminPagedAsync(int page, int pageSize)
        => _sender.Send(new GetAllMealsForAdminPagedQuery(page, pageSize));

    public Task<bool> UpdateMealImageAsync(int mealId, string imageUrl)
        => _sender.Send(new UpdateMealImageCommand(mealId, imageUrl));

    public Task<string?> DeleteMealImageAsync(int mealId)
        => _sender.Send(new DeleteMealImageCommand(mealId));

    public Task<List<MealWithDetailsDto>> GetMealsBatchDetailsForUsersAsync(List<int> mealIds)
        => _sender.Send(new GetMealsBatchDetailsForUsersQuery(mealIds));
}
