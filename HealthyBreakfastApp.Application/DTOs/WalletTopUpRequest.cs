using System;
using System.ComponentModel.DataAnnotations;

namespace HealthyBreakfastApp.Application.DTOs
{
    public class WalletTopUpRequest
    {
        [Required]
        public Guid AuthId { get; set; }

        [Required]
        [Range(1, 10000)]
        public decimal Amount { get; set; }
        
        public string Description { get; set; } = "Wallet top-up";
    }
}
