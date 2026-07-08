using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sovva.Domain.Entities
{
    public class FailedOrderAttempt
    {
        [Key]
        public long Id { get; set; }
        
        [Required]
        public int UserId { get; set; }
        
        [Required]
        public int ScheduledOrderId { get; set; }
        
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal RequiredAmount { get; set; }
        
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal AvailableBalance { get; set; }
        
        [Required]
        [MaxLength(255)]
        public string Reason { get; set; } = null!;
        
        [Required]
        public DateTime AttemptedAt { get; set; }
    }
}
