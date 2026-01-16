using System;
using System.ComponentModel.DataAnnotations;

namespace HealthyBreakfastApp.Domain.Entities
{
    public class User
    {
        [Key]
        public int UserId { get; set; }

        public string Name { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Phone { get; set; } = null!;

        // ✅ NEW FIELDS - Add these
        public string? DeliveryAddress { get; set; }
        public string AccountStatus { get; set; } = "Active"; // "Active", "Deactivated", "Deleted"

        public decimal WalletBalance { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation property
        public virtual UserAuthMapping? AuthMapping { get; set; }
    }
}
