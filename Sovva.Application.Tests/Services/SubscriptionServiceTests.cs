using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Sovva.Application.Services;
using Sovva.Application.Interfaces;
using Sovva.Application.Helpers;
using Sovva.Application.DTOs;
using Sovva.Domain.Entities;
using Sovva.Domain.Enums;
using Sovva.Application.Exceptions;
using Sovva.Domain.Exceptions;
using Sovva.Application.Features.Subscriptions.Commands.ActivateSubscription;
using Sovva.Application.Features.Subscriptions.Commands.CreateSubscription;
using Sovva.Application.Features.Subscriptions.Commands.DeactivateSubscription;
using Sovva.Application.Features.Subscriptions.Commands.DeleteSubscription;
using Sovva.Application.Features.Subscriptions.Commands.ExpireSubscriptions;
using Sovva.Application.Features.Subscriptions.Commands.UpdateNextScheduledDates;
using Sovva.Application.Features.Subscriptions.Commands.UpdateSubscription;
using Sovva.Application.Features.Subscriptions.Queries.GetActiveSubscriptionByUserMealId;
using Sovva.Application.Features.Subscriptions.Queries.GetActiveSubscriptions;
using Sovva.Application.Features.Subscriptions.Queries.GetAllSubscriptions;
using Sovva.Application.Features.Subscriptions.Queries.GetSubscriptionById;
using Sovva.Application.Features.Subscriptions.Queries.GetSubscriptionsByUserId;
using Sovva.Application.Tests.Helpers;

namespace Sovva.Application.Tests.Services
{
    public class SubscriptionServiceTests
    {
        // 1. Declare Mocks for all 12 dependencies
        private readonly Mock<ISubscriptionRepository> _subRepoMock = new();
        private readonly Mock<IUserMealRepository> _userMealRepoMock = new();
        private readonly Mock<IMealRepository> _mealRepoMock = new();
        private readonly Mock<IUserAddressRepository> _addressRepoMock = new();
        private readonly Mock<IScheduledOrderRepository> _scheduledOrderRepoMock = new();
        private readonly Mock<IIngredientRepository> _ingredientRepoMock = new();
        private readonly Mock<IUserMealIngredientRepository> _userMealIngRepoMock = new();
        private readonly Mock<IWalletTransactionService> _walletServiceMock = new();
        private readonly Mock<IAppTimeProvider> _timeProviderMock = new();
        private readonly Mock<ILogger<CreateSubscriptionCommandHandler>> _createLoggerMock = new();
        private readonly Mock<ILogger<DeleteSubscriptionCommandHandler>> _deleteLoggerMock = new();
        private readonly Mock<ILogger<ActivateSubscriptionCommandHandler>> _activateLoggerMock = new();
        private readonly Mock<ILogger<DeactivateSubscriptionCommandHandler>> _deactivateLoggerMock = new();
        private readonly Mock<ILogger<UpdateNextScheduledDatesCommandHandler>> _updateDatesLoggerMock = new();
        private readonly Mock<ILogger<ExpireSubscriptionsCommandHandler>> _expireLoggerMock = new();
        private readonly Mock<IUserLoader> _userLoaderMock = new();
        private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
        private readonly Mock<ICacheService> _cacheServiceMock = new();
        private readonly TestMediatRSender _sender = new();

        // 2. The Service Under Test (SUT)
        private readonly SubscriptionService _sut;

        public SubscriptionServiceTests()
        {
            // 3. Set standard defaults (e.g., Time)
            var today = new DateOnly(2026, 6, 1); // Monday
            var utcNow = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);
            
            _timeProviderMock.Setup(t => t.TodayIst).Returns(today);
            _timeProviderMock.Setup(t => t.UtcNow).Returns(utcNow);
            _timeProviderMock.Setup(t => t.ToIst(It.IsAny<DateTime>())).Returns((DateTime dt) => dt.AddHours(5).AddMinutes(30));
            _timeProviderMock.Setup(t => t.TomorrowIst).Returns(today.AddDays(1));

            // Unit of work mock setup to execute passed actions immediately
            _unitOfWorkMock.Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()))
                           .Returns((Func<Task> action) => action());
                           
            _unitOfWorkMock.Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task<SubscriptionDto>>>()))
                           .Returns((Func<Task<SubscriptionDto>> action) => action());

            // Register handlers
            _sender.Register(new CreateSubscriptionCommandHandler(
                _subRepoMock.Object, _userMealRepoMock.Object, _mealRepoMock.Object, _addressRepoMock.Object,
                _scheduledOrderRepoMock.Object, _ingredientRepoMock.Object, _userMealIngRepoMock.Object,
                _timeProviderMock.Object, _createLoggerMock.Object, _userLoaderMock.Object, _unitOfWorkMock.Object, _cacheServiceMock.Object));
            _sender.Register(new UpdateSubscriptionCommandHandler(_subRepoMock.Object, _unitOfWorkMock.Object, _timeProviderMock.Object, _cacheServiceMock.Object));
            _sender.Register(new DeleteSubscriptionCommandHandler(_subRepoMock.Object, _scheduledOrderRepoMock.Object, _walletServiceMock.Object, _unitOfWorkMock.Object, _cacheServiceMock.Object, _deleteLoggerMock.Object));
            _sender.Register(new ActivateSubscriptionCommandHandler(_subRepoMock.Object, _mealRepoMock.Object, _cacheServiceMock.Object, _activateLoggerMock.Object));
            _sender.Register(new DeactivateSubscriptionCommandHandler(_subRepoMock.Object, _cacheServiceMock.Object, _deactivateLoggerMock.Object));
            _sender.Register(new UpdateNextScheduledDatesCommandHandler(_subRepoMock.Object, _timeProviderMock.Object, _updateDatesLoggerMock.Object));
            _sender.Register(new ExpireSubscriptionsCommandHandler(_subRepoMock.Object, _timeProviderMock.Object, _cacheServiceMock.Object, _expireLoggerMock.Object));

            _sender.Register(new GetAllSubscriptionsQueryHandler(_subRepoMock.Object));
            _sender.Register(new GetSubscriptionByIdQueryHandler(_subRepoMock.Object));
            _sender.Register(new GetSubscriptionsByUserIdQueryHandler(_subRepoMock.Object, _cacheServiceMock.Object));
            _sender.Register(new GetActiveSubscriptionsQueryHandler(_subRepoMock.Object));
            _sender.Register(new GetActiveSubscriptionByUserMealIdQueryHandler(_subRepoMock.Object));

            // 4. Instantiate the service with sender
            _sut = new SubscriptionService(_sender);
        }

        [Fact]
        public async Task GetSubscriptionByIdAsync_ShouldReturnDto_WhenExists()
        {
            // Arrange
            var subId = 99;
            var fakeSubscription = new Subscription 
            { 
                SubscriptionId = subId, 
                UserId = 1,
                IsActive = true,
                User = new User { UserId = 1, Name = "Test" },
                WeeklySchedule = new List<SubscriptionSchedule>()
            };

            _subRepoMock.Setup(x => x.GetByIdAsync(subId))
                        .ReturnsAsync(fakeSubscription);

            // Act
            var result = await _sut.GetSubscriptionByIdAsync(subId);

            // Assert
            result.Should().NotBeNull();
            result!.SubscriptionId.Should().Be(subId);
            result.IsActive.Should().BeTrue();
            
            _subRepoMock.Verify(x => x.GetByIdAsync(subId), Times.Once);
        }

        [Fact]
        public async Task GetAllSubscriptionsAsync_ShouldReturnPagedResult()
        {
            // Arrange
            var fakeSubs = new List<Subscription>
            {
                new Subscription { SubscriptionId = 1, User = new User { Name = "A" }, WeeklySchedule = new List<SubscriptionSchedule>() },
                new Subscription { SubscriptionId = 2, User = new User { Name = "B" }, WeeklySchedule = new List<SubscriptionSchedule>() }
            };

            _subRepoMock.Setup(x => x.GetAllWithCountAsync(1, 50))
                        .ReturnsAsync((fakeSubs, 2));

            // Act
            var result = await _sut.GetAllSubscriptionsAsync(1, 50);

            // Assert
            result.Should().NotBeNull();
            result.TotalCount.Should().Be(2);
            result.Items.Should().HaveCount(2);
        }

        [Fact]
        public async Task CreateSubscriptionAsync_ShouldThrowUserNotFoundException_WhenUserDoesNotExist()
        {
            // Arrange
            var dto = new CreateSubscriptionInternalDto { UserId = 5 };

            _userLoaderMock.Setup(x => x.GetUserWithAuthMappingAsync(dto.UserId))
                           .ReturnsAsync((User?)null);

            // Act
            Func<Task> action = async () => await _sut.CreateSubscriptionAsync(dto);

            // Assert
            await action.Should().ThrowAsync<UserNotFoundException>()
                  .Where(e => e.UserId == 5);
                  
            _subRepoMock.Verify(x => x.CreateAsync(It.IsAny<Subscription>()), Times.Never);
        }

        [Fact]
        public async Task CreateSubscriptionAsync_ShouldThrowUnauthorized_WhenUserIdMismatch()
        {
            // Arrange
            var dto = new CreateSubscriptionInternalDto { UserId = 5 };
            
            // The user fetched is ID 6, but requested for ID 5
            _userLoaderMock.Setup(x => x.GetUserWithAuthMappingAsync(dto.UserId))
                           .ReturnsAsync(new User { UserId = 6 });

            // Act
            Func<Task> action = async () => await _sut.CreateSubscriptionAsync(dto);

            // Assert
            await action.Should().ThrowAsync<UnauthorizedAccessException>();
        }

        [Fact]
        public async Task CreateSubscriptionAsync_ShouldThrowBusinessRuleException_WhenMissingMealAndUserMeal()
        {
            // Arrange
            var dto = new CreateSubscriptionInternalDto 
            { 
                UserId = 1,
                MealId = null,
                UserMealId = null
            };
            
            _userLoaderMock.Setup(x => x.GetUserWithAuthMappingAsync(1))
                           .ReturnsAsync(new User { UserId = 1, AuthMapping = new UserAuthMapping { AuthId = Guid.NewGuid() } });

            // Act
            Func<Task> action = async () => await _sut.CreateSubscriptionAsync(dto);

            // Assert
            await action.Should().ThrowAsync<ArgumentException>()
                  .WithMessage("Either MealId or UserMealId must be provided");
        }

        [Fact]
        public async Task CreateSubscriptionAsync_ShouldThrowBusinessRuleException_WhenBothMealAndUserMealProvided()
        {
            // Arrange
            var dto = new CreateSubscriptionInternalDto 
            { 
                UserId = 1,
                MealId = 10,
                UserMealId = 20
            };
            
            _userLoaderMock.Setup(x => x.GetUserWithAuthMappingAsync(1))
                           .ReturnsAsync(new User { UserId = 1, AuthMapping = new UserAuthMapping { AuthId = Guid.NewGuid() } });

            // Act
            Func<Task> action = async () => await _sut.CreateSubscriptionAsync(dto);

            // Assert
            await action.Should().ThrowAsync<ArgumentException>()
                  .WithMessage("Cannot provide both MealId and UserMealId");
        }

        [Fact]
        public async Task CreateSubscriptionAsync_ShouldThrowDuplicateSubscriptionException_WhenActiveExists()
        {
            // Arrange
            var dto = new CreateSubscriptionInternalDto 
            { 
                UserId = 1,
                MealId = 10
            };
            
            _userLoaderMock.Setup(x => x.GetUserWithAuthMappingAsync(1))
                           .ReturnsAsync(new User { UserId = 1, AuthMapping = new UserAuthMapping { AuthId = Guid.NewGuid() } });
                           
            _mealRepoMock.Setup(x => x.GetByIdAsync(10))
                         .ReturnsAsync(new Meal { MealId = 10, BasePrice = 100m });

            _subRepoMock.Setup(x => x.GetAnyActiveSubscriptionByMealIdAsync(1, 10))
                        .ReturnsAsync(new Subscription()); // Exists

            _addressRepoMock.Setup(x => x.GetPrimaryAddressAsync(1))
                            .ReturnsAsync(new UserAddress { Id = 1, ServiceableLocation = new ServiceableLocation { IsActive = true } });

            // Act
            Func<Task> action = async () => await _sut.CreateSubscriptionAsync(dto);

            // Assert
            await action.Should().ThrowAsync<DuplicateSubscriptionException>();
        }

        [Fact]
        public async Task CreateSubscriptionAsync_ShouldThrowAddressNotFoundException_WhenPrimaryAddressMissing()
        {
            // Arrange
            var dto = new CreateSubscriptionInternalDto 
            { 
                UserId = 1,
                MealId = 10
            };
            
            _userLoaderMock.Setup(x => x.GetUserWithAuthMappingAsync(1))
                           .ReturnsAsync(new User { UserId = 1, AuthMapping = new UserAuthMapping { AuthId = Guid.NewGuid() } });
                           
            _mealRepoMock.Setup(x => x.GetByIdAsync(10))
                         .ReturnsAsync(new Meal { MealId = 10, BasePrice = 100m });

            _subRepoMock.Setup(x => x.GetAnyActiveSubscriptionByMealIdAsync(1, 10))
                        .ReturnsAsync((Subscription?)null);
                        
            _addressRepoMock.Setup(x => x.GetPrimaryAddressAsync(1))
                            .ReturnsAsync((UserAddress?)null);

            // Act
            Func<Task> action = async () => await _sut.CreateSubscriptionAsync(dto);

            // Assert
            await action.Should().ThrowAsync<AddressNotFoundException>();
        }

        [Fact]
        public async Task CreateSubscriptionAsync_ShouldThrowBusinessRuleException_WhenDatesInvalid()
        {
            // Arrange
            var dto = new CreateSubscriptionInternalDto 
            { 
                UserId = 1,
                MealId = 10,
                StartDate = new DateOnly(2026, 6, 10),
                EndDate = new DateOnly(2026, 6, 5) // End before start
            };
            
            _userLoaderMock.Setup(x => x.GetUserWithAuthMappingAsync(1))
                           .ReturnsAsync(new User { UserId = 1, AuthMapping = new UserAuthMapping { AuthId = Guid.NewGuid() } });
                           
            _mealRepoMock.Setup(x => x.GetByIdAsync(10))
                         .ReturnsAsync(new Meal { MealId = 10, BasePrice = 100m });

            _subRepoMock.Setup(x => x.GetAnyActiveSubscriptionByMealIdAsync(1, 10))
                        .ReturnsAsync((Subscription?)null);
                        
            _addressRepoMock.Setup(x => x.GetPrimaryAddressAsync(1))
                            .ReturnsAsync(new UserAddress { Id = 1, ServiceableLocation = new ServiceableLocation { IsActive = true } });

            // Act
            Func<Task> action = async () => await _sut.CreateSubscriptionAsync(dto);

            // Assert
            await action.Should().ThrowAsync<ArgumentException>()
                  .WithMessage("Start date must be before end date");
        }
        
        [Fact]
        public async Task DeleteSubscriptionAsync_ShouldSoftDelete_WhenCalled()
        {
            // Arrange
            int subId = 1;
            var sub = new Subscription { SubscriptionId = subId, IsActive = true };
            
            _subRepoMock.Setup(x => x.GetByIdAsync(subId)).ReturnsAsync(sub);
            _scheduledOrderRepoMock.Setup(x => x.GetBySubscriptionIdAsync(subId))
                                   .ReturnsAsync(new List<ScheduledOrder>());
                                   
            _subRepoMock.Setup(x => x.DeleteAsync(subId)).ReturnsAsync(true);

            // Act
            var result = await _sut.DeleteSubscriptionAsync(subId);

            // Assert
            result.Should().BeTrue();
            sub.IsActive.Should().BeFalse();
            _subRepoMock.Verify(x => x.UpdateAsync(It.IsAny<Subscription>()), Times.Once);
            _subRepoMock.Verify(x => x.DeleteAsync(subId), Times.Once);
        }
    }
}
