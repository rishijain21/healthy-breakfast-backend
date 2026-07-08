using System.Threading.Tasks;
using Sovva.Domain.Entities;

namespace Sovva.Application.Interfaces
{
    public interface IFailedOrderAttemptRepository
    {
        Task AddAsync(FailedOrderAttempt attempt);
    }
}
