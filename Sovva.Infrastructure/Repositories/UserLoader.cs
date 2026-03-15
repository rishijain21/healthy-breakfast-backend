using Sovva.Application.Interfaces;
using Sovva.Domain.Entities;
using Sovva.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace Sovva.Infrastructure.Repositories
{
    public class UserLoader : IUserLoader
    {
        private readonly AppDbContext _context;

        public UserLoader(AppDbContext context)
        {
            _context = context;
        }

        public async Task<User?> GetUserWithAuthMappingAsync(int userId)
        {
            return await _context.Users
                .Include(u => u.AuthMapping)
                .FirstOrDefaultAsync(u => u.UserId == userId);
        }
    }
}
