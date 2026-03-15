namespace Sovva.Application.DTOs
{
    public class UserDto
    {
        public int UserId { get; set; }
        public string Name { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Phone { get; set; } = null!;
        
        // ✅ NEW FIELDS
        public string? DeliveryAddress { get; set; }
        public string AccountStatus { get; set; } = "Active";
        public bool ProfileComplete { get; set; } // Computed property
        
        public decimal WalletBalance { get; set; }

        // ✅ ADD THIS - Role for authorization
        public string Role { get; set; } = "User";

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
