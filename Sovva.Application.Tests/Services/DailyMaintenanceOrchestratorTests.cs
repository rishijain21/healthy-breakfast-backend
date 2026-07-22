using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using Sovva.Application.Interfaces;
using Sovva.Application.Services;
using FluentAssertions;

namespace Sovva.Application.Tests.Services
{
    public class DailyMaintenanceOrchestratorTests
    {
        private readonly Mock<ISubscriptionService> _mockSubscriptionService;
        private readonly Mock<IScheduledOrderService> _mockScheduledOrderService;
        private readonly Mock<ISubscriptionSchedulingService> _mockSubscriptionSchedulingService;
        private readonly Mock<ILogger<DailyMaintenanceOrchestrator>> _mockLogger;
        private readonly DailyMaintenanceOrchestrator _orchestrator;

        public DailyMaintenanceOrchestratorTests()
        {
            _mockSubscriptionService = new Mock<ISubscriptionService>();
            _mockScheduledOrderService = new Mock<IScheduledOrderService>();
            _mockSubscriptionSchedulingService = new Mock<ISubscriptionSchedulingService>();
            _mockLogger = new Mock<ILogger<DailyMaintenanceOrchestrator>>();

            _orchestrator = new DailyMaintenanceOrchestrator(
                _mockSubscriptionService.Object,
                _mockScheduledOrderService.Object,
                _mockSubscriptionSchedulingService.Object,
                _mockLogger.Object);
        }

        [Fact]
        public async Task RunDailyMaintenanceAsync_Step3Fails_Step4StillRuns()
        {
            // Arrange
            _mockSubscriptionService.Setup(s => s.ExpireSubscriptionsAsync()).Returns(Task.CompletedTask);
            _mockSubscriptionService.Setup(s => s.UpdateNextScheduledDatesAsync()).Returns(Task.CompletedTask);
            
            // Step 3 fails
            _mockScheduledOrderService.Setup(s => s.ConfirmAllScheduledOrdersAsync(It.IsAny<DateOnly?>()))
                .ThrowsAsync(new Exception("Database connection failed"));
                
            // Step 4 succeeds
            _mockSubscriptionSchedulingService.Setup(s => s.GenerateScheduledOrdersFromSubscriptionsAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            Func<Task> act = async () => await _orchestrator.RunDailyMaintenanceAsync();

            // Assert
            var exception = await act.Should().ThrowAsync<AggregateException>();
            exception.WithMessage("*Daily Maintenance failed (1/4 steps failed)*");
            exception.Which.InnerExceptions.Should().ContainSingle()
                .Which.Message.Should().Be("Step 3 (ConfirmAllScheduledOrders) failed");

            // Verify Step 4 still ran!
            _mockSubscriptionSchedulingService.Verify(
                s => s.GenerateScheduledOrdersFromSubscriptionsAsync(It.IsAny<string>()), 
                Times.Once);
        }

        [Fact]
        public async Task RunDailyMaintenanceAsync_AllStepsSucceed_DoesNotThrow()
        {
            // Arrange
            _mockSubscriptionService.Setup(s => s.ExpireSubscriptionsAsync()).Returns(Task.CompletedTask);
            _mockSubscriptionService.Setup(s => s.UpdateNextScheduledDatesAsync()).Returns(Task.CompletedTask);
            _mockScheduledOrderService.Setup(s => s.ConfirmAllScheduledOrdersAsync(It.IsAny<DateOnly?>()))
                .ReturnsAsync(new Sovva.Application.DTOs.ProcessOrdersResponseDto { OrdersConfirmed = 10, OrdersFailed = 0 });
            _mockSubscriptionSchedulingService.Setup(s => s.GenerateScheduledOrdersFromSubscriptionsAsync(It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            // Act
            Func<Task> act = async () => await _orchestrator.RunDailyMaintenanceAsync();

            // Assert
            await act.Should().NotThrowAsync();

            _mockSubscriptionService.Verify(s => s.ExpireSubscriptionsAsync(), Times.Once);
            _mockSubscriptionService.Verify(s => s.UpdateNextScheduledDatesAsync(), Times.Once);
            _mockScheduledOrderService.Verify(s => s.ConfirmAllScheduledOrdersAsync(It.IsAny<DateOnly?>()), Times.Once);
            _mockSubscriptionSchedulingService.Verify(s => s.GenerateScheduledOrdersFromSubscriptionsAsync(It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task RunDailyMaintenanceAsync_AllStepsFail_ThrowsAggregateExceptionWith4Errors()
        {
            // Arrange
            _mockSubscriptionService.Setup(s => s.ExpireSubscriptionsAsync())
                .ThrowsAsync(new Exception("Fail 1"));
            _mockSubscriptionService.Setup(s => s.UpdateNextScheduledDatesAsync())
                .ThrowsAsync(new Exception("Fail 2"));
            _mockScheduledOrderService.Setup(s => s.ConfirmAllScheduledOrdersAsync(It.IsAny<DateOnly?>()))
                .ThrowsAsync(new Exception("Fail 3"));
            _mockSubscriptionSchedulingService.Setup(s => s.GenerateScheduledOrdersFromSubscriptionsAsync(It.IsAny<string>()))
                .ThrowsAsync(new Exception("Fail 4"));

            // Act
            Func<Task> act = async () => await _orchestrator.RunDailyMaintenanceAsync();

            // Assert
            var exception = await act.Should().ThrowAsync<AggregateException>();
            exception.WithMessage("*Daily Maintenance failed (4/4 steps failed)*");
            exception.Which.InnerExceptions.Should().HaveCount(4);
        }
    }
}
