using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Sovva.Application.DTOs;
using Sovva.Application.Exceptions;
using Sovva.Application.Interfaces;
using Sovva.Application.Services;
using Sovva.Domain.Constants;
using Sovva.Domain.Entities;
using Sovva.Domain.Enums;
using Xunit;

namespace Sovva.Application.Tests.Services
{
    public class WalletTransactionServiceTests
    {
        private readonly Mock<IWalletTransactionRepository> _walletTxRepoMock;
        private readonly Mock<IUserRepository> _userRepoMock;
        private readonly Mock<IUnitOfWork> _unitOfWorkMock;
        private readonly Mock<ILogger<WalletTransactionService>> _loggerMock;
        private readonly WalletTransactionService _service;

        public WalletTransactionServiceTests()
        {
            _walletTxRepoMock = new Mock<IWalletTransactionRepository>();
            _userRepoMock = new Mock<IUserRepository>();
            _unitOfWorkMock = new Mock<IUnitOfWork>();
            _loggerMock = new Mock<ILogger<WalletTransactionService>>();

            // Mock ExecuteInTransactionAsync to execute the callback immediately and propagate its Task
            _unitOfWorkMock
                .Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>()))
                .Returns<Func<Task>>(action => action());

            _service = new WalletTransactionService(
                _walletTxRepoMock.Object,
                _userRepoMock.Object,
                _unitOfWorkMock.Object,
                _loggerMock.Object
            );
        }

        [Fact]
        public async Task CreateTransactionAsync_ShouldThrowArgumentException_WhenUserDoesNotExist()
        {
            // Arrange
            var dto = new CreateWalletTransactionDto
            {
                UserId = 999,
                Amount = 100,
                Type = WalletConstants.Credit,
                Description = "Top up"
            };

            _userRepoMock.Setup(r => r.GetByIdAsync(dto.UserId))
                .ReturnsAsync((User?)null);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateTransactionAsync(dto));
        }

        [Fact]
        public async Task CreateTransactionAsync_ShouldThrowArgumentException_WhenTypeIsInvalid()
        {
            // Arrange
            var user = new User { UserId = 1, Name = "John Doe", Email = "john@example.com" };
            var dto = new CreateWalletTransactionDto
            {
                UserId = 1,
                Amount = 100,
                Type = "InvalidType",
                Description = "Bad transaction"
            };

            _userRepoMock.Setup(r => r.GetByIdAsync(dto.UserId))
                .ReturnsAsync(user);

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateTransactionAsync(dto));
        }

        [Fact]
        public async Task CreateTransactionAsync_Credit_ShouldThrowInvalidOperationException_WhenBelowMinAmount()
        {
            // Arrange
            var user = new User { UserId = 1, Name = "John Doe", Email = "john@example.com" };
            var dto = new CreateWalletTransactionDto
            {
                UserId = 1,
                Amount = WalletConstants.MinTopUpAmount - 10, // below ₹50
                Type = WalletConstants.Credit,
                Description = "Small top up",
                IsAdminCredit = false
            };

            _userRepoMock.Setup(r => r.GetByIdAsync(dto.UserId))
                .ReturnsAsync(user);

            _walletTxRepoMock.Setup(r => r.GetUserBalanceAsync(dto.UserId))
                .ReturnsAsync(100m);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateTransactionAsync(dto));
            ex.Message.Should().Contain("Minimum top-up amount is");
        }

        [Fact]
        public async Task CreateTransactionAsync_Credit_ShouldSucceed_WhenBelowMinAmountButIsAdminCredit()
        {
            // Arrange
            var user = new User { UserId = 1, Name = "John Doe", Email = "john@example.com" };
            var dto = new CreateWalletTransactionDto
            {
                UserId = 1,
                Amount = 10m, // Below WalletConstants.MinTopUpAmount
                Type = WalletConstants.Credit,
                Description = "Admin Small Refund",
                IsAdminCredit = true
            };

            var createdTx = new WalletTransaction
            {
                TransactionId = 123,
                UserId = dto.UserId,
                Amount = dto.Amount,
                Type = dto.Type,
                Description = dto.Description
            };

            _userRepoMock.Setup(r => r.GetByIdAsync(dto.UserId))
                .ReturnsAsync(user);

            _walletTxRepoMock.Setup(r => r.GetUserBalanceAsync(dto.UserId))
                .ReturnsAsync(100m);

            _walletTxRepoMock.Setup(r => r.CreateAsync(It.IsAny<WalletTransaction>()))
                .ReturnsAsync(createdTx);

            _walletTxRepoMock.Setup(r => r.GetByIdAsync(123))
                .ReturnsAsync(createdTx);

            // Act
            var result = await _service.CreateTransactionAsync(dto);

            // Assert
            result.Should().NotBeNull();
            result.TransactionId.Should().Be(123);
            result.Amount.Should().Be(10m);
            _walletTxRepoMock.Verify(r => r.CreateAsync(It.Is<WalletTransaction>(t => t.Amount == 10m && t.Type == WalletConstants.Credit)), Times.Once);
        }

        [Fact]
        public async Task CreateTransactionAsync_Credit_ShouldThrowInvalidOperationException_WhenExceedsMaxBalance()
        {
            // Arrange
            var user = new User { UserId = 1, Name = "John Doe", Email = "john@example.com" };
            var dto = new CreateWalletTransactionDto
            {
                UserId = 1,
                Amount = 1000m,
                Type = WalletConstants.Credit,
                Description = "Top up",
                IsAdminCredit = false
            };

            _userRepoMock.Setup(r => r.GetByIdAsync(dto.UserId))
                .ReturnsAsync(user);

            // Current balance is already at MaxWalletBalance
            _walletTxRepoMock.Setup(r => r.GetUserBalanceAsync(dto.UserId))
                .ReturnsAsync(WalletConstants.MaxWalletBalance);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.CreateTransactionAsync(dto));
            ex.Message.Should().Contain("Maximum wallet balance is");
        }

        [Fact]
        public async Task CreateTransactionAsync_Debit_ShouldThrowInsufficientBalanceException_WhenBalanceTooLow()
        {
            // Arrange
            var user = new User { UserId = 1, Name = "John Doe", Email = "john@example.com" };
            var dto = new CreateWalletTransactionDto
            {
                UserId = 1,
                Amount = 500m,
                Type = WalletConstants.Debit,
                Description = "Meal purchase"
            };

            _userRepoMock.Setup(r => r.GetByIdAsync(dto.UserId))
                .ReturnsAsync(user);

            // Only ₹100 in the wallet, but trying to debit ₹500
            _walletTxRepoMock.Setup(r => r.GetUserBalanceAsync(dto.UserId))
                .ReturnsAsync(100m);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InsufficientBalanceException>(() => _service.CreateTransactionAsync(dto));
            ex.Required.Should().Be(500m);
            ex.Available.Should().Be(100m);
        }

        [Fact]
        public async Task CreateTransactionAsync_Debit_ShouldSucceed_WhenBalanceIsSufficient()
        {
            // Arrange
            var user = new User { UserId = 1, Name = "John Doe", Email = "john@example.com" };
            var dto = new CreateWalletTransactionDto
            {
                UserId = 1,
                Amount = 150m,
                Type = WalletConstants.Debit,
                Description = "Meal purchase"
            };

            var createdTx = new WalletTransaction
            {
                TransactionId = 456,
                UserId = dto.UserId,
                Amount = dto.Amount,
                Type = dto.Type,
                Description = dto.Description
            };

            _userRepoMock.Setup(r => r.GetByIdAsync(dto.UserId))
                .ReturnsAsync(user);

            _walletTxRepoMock.Setup(r => r.GetUserBalanceAsync(dto.UserId))
                .ReturnsAsync(300m); // plenty of funds

            _walletTxRepoMock.Setup(r => r.CreateAsync(It.IsAny<WalletTransaction>()))
                .ReturnsAsync(createdTx);

            _walletTxRepoMock.Setup(r => r.GetByIdAsync(456))
                .ReturnsAsync(createdTx);

            // Act
            var result = await _service.CreateTransactionAsync(dto);

            // Assert
            result.Should().NotBeNull();
            result.TransactionId.Should().Be(456);
            result.Amount.Should().Be(150m);
            result.Type.Should().Be(WalletConstants.Debit);

            _walletTxRepoMock.Verify(r => r.AcquireUserWalletLockAsync(dto.UserId), Times.Once);
            _walletTxRepoMock.Verify(r => r.CreateAsync(It.Is<WalletTransaction>(t => t.Amount == 150m && t.Type == WalletConstants.Debit)), Times.Once);
        }

        [Fact]
        public async Task TopUpWalletAsync_ShouldThrowInvalidOperationException_WhenAmountTooLow()
        {
            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.TopUpWalletAsync(1, 10m));
            ex.Message.Should().Contain("Minimum top-up amount is");
        }

        [Fact]
        public async Task TopUpWalletAsync_ShouldThrowInvalidOperationException_WhenExceedsMaxBalance()
        {
            // Arrange
            _walletTxRepoMock.Setup(r => r.GetUserBalanceAsync(1))
                .ReturnsAsync(WalletConstants.MaxWalletBalance - 10m);

            // Act & Assert
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _service.TopUpWalletAsync(1, 100m));
            ex.Message.Should().Contain("Maximum wallet balance is");
        }

        [Fact]
        public async Task AdminCreditWalletAsync_ShouldSucceed_WhenValid()
        {
            // Arrange
            var user = new User { UserId = 1, Name = "John Doe", Email = "john@example.com", Phone = "9876543210", Role = UserRole.Customer };
            
            _userRepoMock.Setup(r => r.GetByIdAsync(1))
                .ReturnsAsync(user);

            _walletTxRepoMock.Setup(r => r.GetUserBalanceAsync(1))
                .ReturnsAsync(100m);

            var createdTx = new WalletTransaction
            {
                TransactionId = 789,
                UserId = 1,
                Amount = 15m, // Below minimum limit but fine for admin credit
                Type = WalletConstants.Credit,
                Description = "Refund"
            };

            _walletTxRepoMock.Setup(r => r.CreateAsync(It.IsAny<WalletTransaction>()))
                .ReturnsAsync(createdTx);

            _walletTxRepoMock.Setup(r => r.GetByIdAsync(789))
                .ReturnsAsync(createdTx);

            // Act
            var result = await _service.AdminCreditWalletAsync(1, 15m, "Refund");

            // Assert
            result.Should().NotBeNull();
            result.TransactionId.Should().Be(789);
            result.Amount.Should().Be(15m);
        }

        [Fact]
        public async Task WriteTransactionRecordAsync_ShouldInvokeRepositoryCorrectly()
        {
            // Act
            await _service.WriteTransactionRecordAsync(1, 100m, WalletConstants.Debit, "Audit log", 55);

            // Assert
            _walletTxRepoMock.Verify(r => r.WriteRecordOnlyAsync(It.Is<WalletTransaction>(t =>
                t.UserId == 1 &&
                t.Amount == 100m &&
                t.Type == WalletConstants.Debit &&
                t.Description == "Audit log" &&
                t.ScheduledOrderId == 55
            )), Times.Once);
        }
    }
}
