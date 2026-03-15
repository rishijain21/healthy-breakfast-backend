using System.ComponentModel.DataAnnotations;

namespace Sovva.Application.DTOs
{
    public class WalletTopUpDto
    {
        [Required]
        [Range(1, 10000)]
        public decimal Amount { get; set; }
        
        public string? Description { get; set; } = "Wallet top-up";
    }
}
