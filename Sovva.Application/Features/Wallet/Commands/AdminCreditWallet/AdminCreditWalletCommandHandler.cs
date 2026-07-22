using System;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using Sovva.Application.DTOs;
using Sovva.Application.Features.Wallet.Commands.CreateWalletTransaction;
using Sovva.Application.Interfaces;
using Sovva.Domain.Constants;

namespace Sovva.Application.Features.Wallet.Commands.AdminCreditWallet;

public class AdminCreditWalletCommandHandler : IRequestHandler<AdminCreditWalletCommand, WalletTransactionDto>
{
    private readonly IUserRepository _userRepository;
    private readonly ISender _sender;

    public AdminCreditWalletCommandHandler(IUserRepository userRepository, ISender sender)
    {
        _userRepository = userRepository;
        _sender = sender;
    }

    public async Task<WalletTransactionDto> Handle(AdminCreditWalletCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdAsync((int)request.UserId);
        if (user == null)
        {
            throw new InvalidOperationException("User not found");
        }

        if (request.Amount <= 0)
        {
            throw new ArgumentException("Amount must be greater than zero");
        }

        return await _sender.Send(new CreateWalletTransactionCommand(new CreateWalletTransactionDto
        {
            UserId = (int)request.UserId,
            Amount = request.Amount,
            Type = WalletConstants.Credit,
            Description = request.Description,
            IsAdminCredit = true,
            ReferenceType = "Manual",
            ReferenceId = request.AdminUserId
        }), cancellationToken);
    }
}
