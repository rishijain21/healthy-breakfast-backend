using System.ComponentModel.DataAnnotations;

namespace Sovva.Application.DTOs
{
    public class CreateWalletTransactionDto
    {
        [Required]
        public int UserId { get; set; }

        [Required]
        [Range(0.01, 999999.99, ErrorMessage = "Amount must be greater than 0")]
        public decimal Amount { get; set; }

        [Required]
        [StringLength(20)]
        public string Type { get; set; } = null!; // "Credit" or "Debit"

        [Required]
        [StringLength(500)]
        public string Description { get; set; } = null!;
    }
}
