using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HealthyBreakfastApp.Domain.Entities
{
    public class UserAuthMapping
    {
        // NEW: Add integer primary key for performance
        [Key]
        public int MappingId { get; set; }

        // CHANGE: Make AuthId indexed but not PK
        [Required]
        public Guid AuthId { get; set; }

        public int UserId { get; set; }

        public DateTime CreatedAt { get; set; }

        // Navigation property
        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;
    }
}
