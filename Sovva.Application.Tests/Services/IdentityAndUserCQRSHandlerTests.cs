using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Sovva.Application.Common.Infrastructure;
using Sovva.Application.DTOs;
using Sovva.Application.Features.Identity.Commands.RegisterUser;
using Sovva.Application.Features.Identity.Queries.GetUserByAuthId;
using Sovva.Application.Features.Identity.Queries.GetUserByEmail;
using Sovva.Application.Features.Identity.Queries.UserExists;
using Sovva.Application.Features.Users.Commands.CreateUser;
using Sovva.Application.Features.Users.Commands.DeleteAccount;
using Sovva.Application.Features.Users.Commands.UpdateUserProfile;
using Sovva.Application.Features.Users.Commands.UpdateUserRole;
using Sovva.Application.Features.Users.Queries.GetUserById;
using Sovva.Application.Helpers;
using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;
using Sovva.Domain.Enums;
using Xunit;

namespace Sovva.Application.Tests.Services
{
    public class IdentityAndUserCQRSHandlerTests
    {
        private readonly Mock<IUserRepository> _mockUserRepo;
        private readonly Mock<ICurrentUserService> _mockCurrentUserService;
        private readonly Mock<IAppTimeProvider> _mockTime;
        private readonly Mock<IUnitOfWork> _mockUow;
        private readonly Mock<ICacheService> _mockCache;
        private readonly Mock<ILogger<RegisterUserCommandHandler>> _mockRegLogger;
        private readonly Mock<ILogger<DeleteAccountCommandHandler>> _mockDelLogger;

        public IdentityAndUserCQRSHandlerTests()
        {
            _mockUserRepo = new Mock<IUserRepository>();
            _mockCurrentUserService = new Mock<ICurrentUserService>();
            _mockTime = new Mock<IAppTimeProvider>();
            _mockUow = new Mock<IUnitOfWork>();
            _mockCache = new Mock<ICacheService>();
            _mockRegLogger = new Mock<ILogger<RegisterUserCommandHandler>>();
            _mockDelLogger = new Mock<ILogger<DeleteAccountCommandHandler>>();

            _mockTime.Setup(t => t.UtcNow).Returns(new DateTime(2026, 7, 12, 12, 0, 0, DateTimeKind.Utc));
        }

        [Fact]
        public async Task RegisterUser_NewUser_CreatesAndReturnsUserDto()
        {
            // Arrange
            var authId = Guid.NewGuid();
            var request = new RegisterUserRequest
            {
                AuthId = authId,
                Email = "newuser@example.com",
                Name = "John Doe",
                Phone = "9999999999"
            };

            _mockUserRepo.Setup(r => r.GetByEmailAsync(request.Email)).ReturnsAsync((User?)null);
            _mockUserRepo.Setup(r => r.GetUserByAuthIdIncludingDeletedAsync(authId)).ReturnsAsync((User?)null);

            var createdUser = new User
            {
                UserId = 101,
                Name = request.Name,
                Email = request.Email,
                Phone = request.Phone,
                AccountStatus = AccountStatus.Active,
                Role = UserRole.Customer,
                CreatedAt = _mockTime.Object.UtcNow,
                UpdatedAt = _mockTime.Object.UtcNow
            };

            _mockUserRepo.Setup(r => r.CreateUserWithAuthMappingAsync(It.IsAny<User>(), authId))
                .ReturnsAsync(createdUser);

            var handler = new RegisterUserCommandHandler(
                _mockUserRepo.Object,
                _mockTime.Object,
                _mockUow.Object,
                _mockRegLogger.Object);

            // Act
            var result = await handler.Handle(new RegisterUserCommand(request), CancellationToken.None);

            // Assert
            Assert.NotNull(result);
            Assert.Equal(101, result.UserId);
            Assert.Equal("John Doe", result.Name);
            Assert.Equal("Active", result.AccountStatus);
            _mockUserRepo.Verify(r => r.CreateUserWithAuthMappingAsync(It.IsAny<User>(), authId), Times.Once);
        }

        [Fact]
        public async Task RegisterUser_DeletedUser_ReactivatesAndSaves()
        {
            // Arrange
            var authId = Guid.NewGuid();
            var request = new RegisterUserRequest
            {
                AuthId = authId,
                Email = "deleted@example.com",
                Name = "John Reactivated",
                Phone = "8888888888"
            };

            var existingDeletedUser = new User
            {
                UserId = 50,
                Name = "Old Name",
                Email = request.Email,
                Phone = "1111111111",
                AccountStatus = AccountStatus.Deleted,
                DeletedAt = _mockTime.Object.UtcNow.AddDays(-10)
            };

            _mockUserRepo.Setup(r => r.GetByEmailAsync(request.Email)).ReturnsAsync(existingDeletedUser);
            _mockUserRepo.Setup(r => r.GetUserByAuthIdIncludingDeletedAsync(authId)).ReturnsAsync(existingDeletedUser);

            var handler = new RegisterUserCommandHandler(
                _mockUserRepo.Object,
                _mockTime.Object,
                _mockUow.Object,
                _mockRegLogger.Object);

            // Act
            var result = await handler.Handle(new RegisterUserCommand(request), CancellationToken.None);

            // Assert
            Assert.Equal(50, result.UserId);
            Assert.Equal("Active", result.AccountStatus);
            Assert.Equal("John Reactivated", existingDeletedUser.Name);
            Assert.Null(existingDeletedUser.DeletedAt);
            _mockUserRepo.Verify(r => r.UpdateUserAsync(existingDeletedUser), Times.Once);
            _mockUow.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task RegisterUser_ActiveEmailExists_ThrowsInvalidOperationException()
        {
            // Arrange
            var request = new RegisterUserRequest
            {
                AuthId = Guid.NewGuid(),
                Email = "existing@example.com",
                Name = "Duplicate User"
            };

            _mockUserRepo.Setup(r => r.GetByEmailAsync(request.Email)).ReturnsAsync(new User
            {
                UserId = 10,
                Email = request.Email,
                AccountStatus = AccountStatus.Active
            });

            var handler = new RegisterUserCommandHandler(
                _mockUserRepo.Object,
                _mockTime.Object,
                _mockUow.Object,
                _mockRegLogger.Object);

            // Act & Assert
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handler.Handle(new RegisterUserCommand(request), CancellationToken.None));
        }

        [Fact]
        public async Task CreateUser_ValidDto_CreatesAndReturnsId()
        {
            // Arrange
            var dto = new CreateUserDto
            {
                Name = "Admin Created",
                Email = "admincreated@example.com",
                Phone = "7777777777"
            };

            _mockUserRepo.Setup(r => r.AddUserAsync(It.IsAny<User>()))
                .Callback<User>(u => u.UserId = 200)
                .Returns(Task.CompletedTask);

            var handler = new CreateUserCommandHandler(
                _mockUserRepo.Object,
                _mockTime.Object,
                _mockUow.Object);

            // Act
            var userId = await handler.Handle(new CreateUserCommand(dto), CancellationToken.None);

            // Assert
            Assert.Equal(200, userId);
            _mockUow.Verify(u => u.SaveChangesAsync(), Times.Once);
        }

        [Fact]
        public async Task UpdateUserProfile_ValidAuthId_UpdatesAndInvalidatesCache()
        {
            // Arrange
            var authId = Guid.NewGuid();
            var existingUser = new User
            {
                UserId = 15,
                Name = "Old Name",
                Phone = "1234567890"
            };

            _mockUserRepo.Setup(r => r.GetUserByAuthIdAsync(authId)).ReturnsAsync(existingUser);

            var dto = new UpdateUserProfileDto
            {
                Name = "New Name",
                Phone = "9876543210"
            };

            var handler = new UpdateUserProfileCommandHandler(
                _mockUserRepo.Object,
                _mockTime.Object,
                _mockUow.Object,
                _mockCache.Object);

            // Act
            var result = await handler.Handle(new UpdateUserProfileCommand(authId, dto), CancellationToken.None);

            // Assert
            Assert.Equal("New Name", result.Name);
            Assert.Equal("9876543210", result.Phone);
            _mockUserRepo.Verify(r => r.UpdateUserAsync(existingUser), Times.Once);
            _mockUow.Verify(u => u.SaveChangesAsync(), Times.Once);
            _mockCache.Verify(c => c.RemoveAsync(It.IsAny<string>()), Times.AtLeast(3));
        }

        [Fact]
        public async Task UpdateUserRole_ValidRole_UpdatesRoleAndReturnsTrue()
        {
            // Arrange
            var existingUser = new User
            {
                UserId = 20,
                Role = UserRole.Customer
            };

            _mockUserRepo.Setup(r => r.GetByIdAsync(20)).ReturnsAsync(existingUser);

            var handler = new UpdateUserRoleCommandHandler(
                _mockUserRepo.Object,
                _mockCurrentUserService.Object,
                _mockTime.Object,
                _mockUow.Object,
                _mockCache.Object);

            // Act
            var result = await handler.Handle(new UpdateUserRoleCommand(20, "Admin"), CancellationToken.None);

            // Assert
            Assert.True(result);
            Assert.Equal(UserRole.Admin, existingUser.Role);
            _mockUserRepo.Verify(r => r.UpdateUserAsync(existingUser), Times.Once);
            _mockUow.Verify(u => u.SaveChangesAsync(), Times.Once);
            _mockCurrentUserService.Verify(c => c.InvalidateCacheAsync(20), Times.Once);
        }

        [Fact]
        public async Task DeleteAccount_ExistingUser_SoftDeletesAndReturnsTrue()
        {
            // Arrange
            var existingUser = new User
            {
                UserId = 30,
                Email = "todelete@example.com",
                AccountStatus = AccountStatus.Active
            };

            _mockUserRepo.Setup(r => r.GetByIdAsync(30)).ReturnsAsync(existingUser);

            var handler = new DeleteAccountCommandHandler(
                _mockUserRepo.Object,
                _mockCurrentUserService.Object,
                _mockTime.Object,
                _mockUow.Object,
                _mockCache.Object,
                _mockDelLogger.Object);

            // Act
            var result = await handler.Handle(new DeleteAccountCommand(30), CancellationToken.None);

            // Assert
            Assert.True(result);
            Assert.Equal(AccountStatus.Deleted, existingUser.AccountStatus);
            Assert.NotNull(existingUser.DeletedAt);
            _mockUserRepo.Verify(r => r.UpdateUserAsync(existingUser), Times.Once);
            _mockUow.Verify(u => u.SaveChangesAsync(), Times.Once);
            _mockCurrentUserService.Verify(c => c.InvalidateCacheAsync(30), Times.Once);
        }

        [Fact]
        public async Task Queries_WorkExpectedly()
        {
            // Arrange
            var user = new User { UserId = 40, Email = "test@example.com", Name = "Test" };
            _mockUserRepo.Setup(r => r.GetByIdAsync(40)).ReturnsAsync(user);
            _mockUserRepo.Setup(r => r.GetByEmailAsync("test@example.com")).ReturnsAsync(user);
            _mockUserRepo.Setup(r => r.GetByEmailAsync("notfound@example.com")).ReturnsAsync((User?)null);

            var byIdHandler = new GetUserByIdQueryHandler(_mockUserRepo.Object, _mockCache.Object);
            var byEmailHandler = new GetUserByEmailQueryHandler(_mockUserRepo.Object);
            var existsHandler = new UserExistsQueryHandler(_mockUserRepo.Object);

            // Act & Assert
            var dtoById = await byIdHandler.Handle(new GetUserByIdQuery(40), CancellationToken.None);
            Assert.NotNull(dtoById);
            Assert.Equal("Test", dtoById.Name);

            var dtoByEmail = await byEmailHandler.Handle(new GetUserByEmailQuery("test@example.com"), CancellationToken.None);
            Assert.NotNull(dtoByEmail);

            var existsTrue = await existsHandler.Handle(new UserExistsQuery("test@example.com"), CancellationToken.None);
            Assert.True(existsTrue);

            var existsFalse = await existsHandler.Handle(new UserExistsQuery("notfound@example.com"), CancellationToken.None);
            Assert.False(existsFalse);
        }
    }
}
