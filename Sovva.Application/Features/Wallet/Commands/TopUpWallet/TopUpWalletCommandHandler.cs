using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.DTOs;
using Sovva.Application.Features.Wallet.Commands.CreateWalletTransaction;
using Sovva.Application.Interfaces;
using Sovva.Domain.Constants;

namespace Sovva.Application.Features.Wallet.Commands.TopUpWallet;

public class TopUpWalletCommandHandler : 
    IRequestHandler<TopUpWalletCommand, UserDto>,
    IRequestHandler<TopUpWalletByDtoCommand, WalletTransactionDto>
{
    private readonly ISender _sender;
    private readonly IUserRepository _userRepository;
    private readonly IWalletTransactionRepository _walletTransactionRepository;

    public TopUpWalletCommandHandler(
        ISender sender,
        IUserRepository userRepository,
        IWalletTransactionRepository walletTransactionRepository)
    {
        _sender = sender;
        _userRepository = userRepository;
        _walletTransactionRepository = walletTransactionRepository;
    }

    public async Task<UserDto> Handle(TopUpWalletCommand request, CancellationToken cancellationToken)
    {
        // Validate minimum amount before sending create transaction command
        if (request.Amount < WalletConstants.MinTopUpAmount)
            throw new InvalidOperationException($"Minimum top-up amount is ₹{WalletConstants.MinTopUpAmount}");

        // CreateTransactionCommand handles advisory lock and MaxWalletBalance check safely
        await _sender.Send(new CreateWalletTransactionCommand(new CreateWalletTransactionDto
        {
            UserId = request.UserId,
            Amount = request.Amount,
            Type = WalletConstants.Credit,
            Description = request.Description
        }), cancellationToken);

        var user = await _userRepository.GetByIdAsync(request.UserId);
        if (user == null) throw new ArgumentException("User not found");

        return new UserDto
        {
            UserId = user.UserId,
            Name = user.Name,
            Email = user.Email,
            AccountStatus = user.AccountStatus.ToString(),
            Role = user.Role.ToString(),
            IsProfileComplete = !string.IsNullOrWhiteSpace(user.Name) &&
                              !string.IsNullOrWhiteSpace(user.Phone)
        };
    }

    public async Task<WalletTransactionDto> Handle(TopUpWalletByDtoCommand request, CancellationToken cancellationToken)
    {
        return await _sender.Send(new CreateWalletTransactionCommand(new CreateWalletTransactionDto
        {
            UserId = request.UserId,
            Amount = request.TopUpDto.Amount,
            Type = WalletConstants.Credit,
            Description = request.TopUpDto.Description ?? $"Wallet top-up of ₹{request.TopUpDto.Amount}"
        }), cancellationToken);
    }
}
