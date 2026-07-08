using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Sovva.Domain.Enums;

namespace Sovva.Domain.Entities
{
    public class User : BaseEntity
    {
        [Key]
        public int UserId { get; set; }

        public string Name { get; set; } = null!;
        public string Email { get; set; } = null!;
        public string Phone { get; set; } = null!;

        public ICollection<UserAddress> Addresses { get; set; } = new List<UserAddress>();

        public AccountStatus AccountStatus { get; set; } = AccountStatus.Active;


        public UserRole Role { get; set; } = UserRole.Customer;

        public DateTime? DeletedAt { get; set; } // Soft delete

        // Navigation property
        public virtual UserAuthMapping? AuthMapping { get; set; }
    }
}
