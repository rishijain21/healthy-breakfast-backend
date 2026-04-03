namespace Sovva.Application.DTOs
{
    public class UserDto
    {
        public int UserId { get; set; }
        public string Name { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Phone { get; set; } = null!;
        public string AccountStatus { get; set; } = "Active";
        public bool ProfileComplete { get; set; }
        public decimal WalletBalance { get; set; }
        public string Role { get; set; } = "Customer";
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}