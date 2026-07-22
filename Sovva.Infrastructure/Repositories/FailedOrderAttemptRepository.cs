using System.Threading.Tasks;
using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;
using Sovva.Infrastructure.Data;

namespace Sovva.Infrastructure.Repositories
{
    public class FailedOrderAttemptRepository : IFailedOrderAttemptRepository
    {
        private readonly AppDbContext _context;

        public FailedOrderAttemptRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task AddAsync(FailedOrderAttempt attempt)
        {
            _context.FailedOrderAttempts.Add(attempt);
            // NOTE: Caller (UnitOfWork or service) is responsible for SaveChangesAsync
        }
    }
}
