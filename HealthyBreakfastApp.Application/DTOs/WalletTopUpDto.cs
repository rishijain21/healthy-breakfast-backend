using System.ComponentModel.DataAnnotations;

namespace HealthyBreakfastApp.Application.DTOs
{
    public class WalletTopUpDto
    {
        [Required]
        [Range(1.00, 10000.00, ErrorMessage = "Top-up amount must be between $1 and $10,000")]
        public decimal Amount { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }
    }
}
