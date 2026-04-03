using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Sovva.Domain.Enums;

namespace Sovva.Domain.Entities
{
    public class User
    {
        [Key]
        public int UserId { get; set; }

        public string Name { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Phone { get; set; } = null!;

        public ICollection<UserAddress> Addresses { get; set; } = new List<UserAddress>();

        public string AccountStatus { get; set; } = "Active"; // "Active", "Deactivated", "Deleted"

        public decimal WalletBalance { get; set; }

        public UserRole Role { get; set; } = UserRole.Customer;

        public DateTime? DeletedAt { get; set; } // Soft delete

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        // Navigation property
        public virtual UserAuthMapping? AuthMapping { get; set; }
    }
}
