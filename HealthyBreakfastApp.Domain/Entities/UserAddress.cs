using System.ComponentModel.DataAnnotations;

namespace HealthyBreakfastApp.Domain.Entities
{
    public class UserAddress
    {
        public int Id { get; set; }
        
        [Required]
        public int UserId { get; set; }
        
        [Required]
        public int ServiceableLocationId { get; set; }
        
        // Building details
        [MaxLength(50)]
        public string? Wing { get; set; }
        
        [MaxLength(50)]
        public string? Block { get; set; }
        
        [Required]
        [MaxLength(50)]
        public string FlatNumber { get; set; } = string.Empty;
        
        [MaxLength(20)]
        public string? Floor { get; set; }
        
        // Additional delivery instructions
        [MaxLength(500)]
        public string? AdditionalInstructions { get; set; }
        
        // Address label for easy identification
        [MaxLength(50)]
        public string? Label { get; set; } // e.g., "Home", "Office"
        
        public bool IsPrimary { get; set; } = false;
        public bool IsActive { get; set; } = true;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        
        // Navigation Properties
        public User User { get; set; } = null!;
        public ServiceableLocation ServiceableLocation { get; set; } = null!;
        public ICollection<Order> Orders { get; set; } = new List<Order>();
        public ICollection<Subscription> Subscriptions { get; set; } = new List<Subscription>();
        
        // Computed property for complete address
        public string CompleteAddress
        {
            get
            {
                var parts = new List<string>();
                
                if (!string.IsNullOrEmpty(FlatNumber))
                    parts.Add($"Flat {FlatNumber}");
                
                if (!string.IsNullOrEmpty(Floor))
                    parts.Add($"Floor {Floor}");
                
                if (!string.IsNullOrEmpty(Wing))
                    parts.Add($"Wing {Wing}");
                
                if (!string.IsNullOrEmpty(Block))
                    parts.Add($"Block {Block}");
                
                return string.Join(", ", parts);
            }
        }
    }
}
