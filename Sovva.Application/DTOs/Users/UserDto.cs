using Sovva.Domain.Constants;

namespace Sovva.Application.DTOs
{
    public class UserDto
    {
        public int UserId { get; set; }
        public string Name { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Phone { get; set; } = null!;
        public string AccountStatus { get; set; } = AccountStatusConstants.Active;
        public bool IsProfileComplete { get; set; }
        public decimal WalletBalance { get; set; }
        public string Role { get; set; } = RoleConstants.Customer;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}