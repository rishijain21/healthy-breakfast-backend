using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.Common.Infrastructure;
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;

namespace Sovva.Application.Features.Catalog.Commands.UpdateMeal;

public class UpdateMealCommandHandler : IRequestHandler<UpdateMealCommand, bool>
{
    private readonly IMealRepository _mealRepository;
    private readonly IIngredientRepository _ingredientRepository;
    private readonly IMealOptionRepository _mealOptionRepository;
    private readonly IMealOptionIngredientRepository _mealOptionIngredientRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICacheService _cacheService;
    private readonly IAppTimeProvider _time;

    public UpdateMealCommandHandler(
        IMealRepository mealRepository,
        IIngredientRepository ingredientRepository,
        IMealOptionRepository mealOptionRepository,
        IMealOptionIngredientRepository mealOptionIngredientRepository,
        IUnitOfWork unitOfWork,
        ICacheService cacheService,
        IAppTimeProvider time)
    {
        _mealRepository = mealRepository;
        _ingredientRepository = ingredientRepository;
        _mealOptionRepository = mealOptionRepository;
        _mealOptionIngredientRepository = mealOptionIngredientRepository;
        _unitOfWork = unitOfWork;
        _cacheService = cacheService;
        _time = time;
    }

    public async Task<bool> Handle(UpdateMealCommand request, CancellationToken cancellationToken)
    {
        var meal = await _mealRepository.GetByIdWithOptionsAsync(request.MealId);
        if (meal == null) return false;

        var dto = request.Dto;

        // Batch-validate all ingredients in a single query (kills N+1)
        var allIngredientIds = dto.MealOptions
            .SelectMany(o => o.IngredientIds)
            .Distinct()
            .ToList();
        var existingIngredients = await _ingredientRepository.GetByIdsAsync(allIngredientIds);
        var missingIds = allIngredientIds.Where(id => !existingIngredients.ContainsKey(id)).ToList();
        if (missingIds.Any())
            throw new ArgumentException($"Ingredients not found: {string.Join(", ", missingIds)}");

        // Update meal basic info
        meal.MealName = dto.MealName;
        meal.Description = dto.Description;
        meal.BasePrice = dto.BasePrice;
        meal.ApproxCalories = dto.ApproxCalories;
        meal.ApproxProtein = dto.ApproxProtein;
        meal.ApproxCarbs = dto.ApproxCarbs;
        meal.ApproxFats = dto.ApproxFats;
        meal.UpdatedAt = _time.UtcNow;

        // Delete existing meal options and their ingredients
        var existingOptions = await _mealOptionRepository.GetByMealIdAsync(request.MealId);
        foreach (var option in existingOptions)
        {
            await _mealOptionIngredientRepository.DeleteByMealOptionIdAsync(option.MealOptionId);
            await _mealOptionRepository.DeleteAsync(option);
        }
        await _unitOfWork.SaveChangesAsync();

        // Create new meal options
        foreach (var optionDto in dto.MealOptions)
        {
            var mealOption = new MealOption
            {
                MealId = meal.MealId,
                CategoryId = optionDto.CategoryId,
                IsRequired = optionDto.IsRequired,
                MaxSelectable = optionDto.MaxSelectable,
                CreatedAt = _time.UtcNow,
                UpdatedAt = _time.UtcNow
            };

            await _mealOptionRepository.AddAsync(mealOption);
            await _unitOfWork.SaveChangesAsync();

            foreach (var ingredientId in optionDto.IngredientIds)
            {
                var mealOptionIngredient = new MealOptionIngredient
                {
                    MealOptionId = mealOption.MealOptionId,
                    IngredientId = ingredientId,
                    CreatedAt = _time.UtcNow,
                    UpdatedAt = _time.UtcNow
                };

                await _mealOptionIngredientRepository.AddAsync(mealOptionIngredient);
            }

            await _unitOfWork.SaveChangesAsync();
        }

        await _mealRepository.UpdateMealAsync(meal);
        await MealCatalogHelper.InvalidateMealCachesAsync(_cacheService, request.MealId);
        return true;
    }
}
