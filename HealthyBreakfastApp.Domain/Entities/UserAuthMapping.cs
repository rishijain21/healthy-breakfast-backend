using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace HealthyBreakfastApp.Domain.Entities
{
    [Table("user_auth_mapping")]  // ✅ Map to lowercase table name
    public class UserAuthMapping
    {
        [Key]
        [Column("mapping_id")]  // ✅ Map to lowercase column
        public int MappingId { get; set; }

        [Required]
        [Column("auth_id")]  // ✅ Map to lowercase column
        public Guid AuthId { get; set; }

        [Column("user_id")]  // ✅ Map to lowercase column
        public int UserId { get; set; }

        [Column("created_at")]  // ✅ Map to lowercase column
        public DateTime CreatedAt { get; set; }

        // Navigation property
        [ForeignKey("UserId")]
        public virtual User? User { get; set; }
    }
}
