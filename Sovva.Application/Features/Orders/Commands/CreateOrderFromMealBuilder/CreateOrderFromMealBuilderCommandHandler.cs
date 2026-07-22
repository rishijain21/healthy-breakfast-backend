using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.DTOs;
using Sovva.Application.Exceptions;
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;

namespace Sovva.Application.Features.Orders.Commands.CreateOrderFromMealBuilder;

public class CreateOrderFromMealBuilderCommandHandler : IRequestHandler<CreateOrderFromMealBuilderCommand, OrderCreationResponseDto>
{
    private readonly IMealService _mealService;
    private readonly IWalletTransactionService _walletService;
    private readonly IUserMealService _userMealService;
    private readonly IUserMealIngredientService _userMealIngredientService;
    private readonly IUserAddressRepository _userAddressRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAppTimeProvider _time;

    public CreateOrderFromMealBuilderCommandHandler(
        IMealService mealService,
        IWalletTransactionService walletService,
        IUserMealService userMealService,
        IUserMealIngredientService userMealIngredientService,
        IUserAddressRepository userAddressRepository,
        IOrderRepository orderRepository,
        IUnitOfWork unitOfWork,
        IAppTimeProvider time)
    {
        _mealService = mealService;
        _walletService = walletService;
        _userMealService = userMealService;
        _userMealIngredientService = userMealIngredientService;
        _userAddressRepository = userAddressRepository;
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _time = time;
    }

    public async Task<OrderCreationResponseDto> Handle(CreateOrderFromMealBuilderCommand request, CancellationToken cancellationToken)
    {
        var dto = request.Dto;
        var userId = request.UserId;

        if (dto.MealId > 0)
        {
            var meal = await _mealService.GetMealByIdAsync(dto.MealId);
            if (meal == null)
                throw new InvalidOperationException("The selected meal is no longer available.");
        }

        int? addressIdToUse = request.DeliveryAddressId;

        if (addressIdToUse.HasValue)
        {
            var address = await _userAddressRepository.GetByIdWithDetailsAsync(addressIdToUse.Value);
            if (address == null || address.UserId != userId)
            {
                throw new InvalidOperationException("Invalid delivery address");
            }

            if (address.ServiceableLocation == null || !address.ServiceableLocation.IsActive)
            {
                throw new InvalidOperationException(
                    $"Sorry, we don't currently deliver to {address.ServiceableLocation?.Area ?? "your location"}. " +
                    "Please update your delivery address."
                );
            }
        }
        else
        {
            var primaryAddress = await _userAddressRepository.GetPrimaryAddressByUserIdAsync(userId);
            if (primaryAddress == null)
            {
                throw new AddressNotFoundException(userId);
            }

            if (primaryAddress.ServiceableLocation == null || !primaryAddress.ServiceableLocation.IsActive)
            {
                throw new InvalidOperationException(
                    $"Sorry, we don't currently deliver to {primaryAddress.ServiceableLocation?.Area ?? "your location"}. " +
                    "Please update your delivery address to a serviceable location."
                );
            }

            addressIdToUse = primaryAddress.Id;
        }

        return await OrdersHelper.ExecuteOrderCreationAsync(
            userId: userId,
            mealId: dto.MealId,
            ingredients: dto.SelectedIngredients,
            deliveryAddressId: addressIdToUse.Value,
            overrideTotalPrice: null,
            scheduledFor: dto.ScheduledFor,
            mealName: dto.MealName,
            mealService: _mealService,
            walletService: _walletService,
            userMealService: _userMealService,
            userMealIngredientService: _userMealIngredientService,
            orderRepository: _orderRepository,
            unitOfWork: _unitOfWork,
            time: _time);
    }
}
