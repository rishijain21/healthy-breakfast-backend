using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Sovva.Domain.Entities
{
    public class WalletTransaction
    {
        [Key]
        public int TransactionId { get; set; }

        [ForeignKey("User")]
        public int UserId { get; set; }

        public decimal Amount { get; set; }
        public string Type { get; set; } = null!;  // Credit or Debit
        public string Description { get; set; } = null!;

        // What triggered this transaction — "Order", "Subscription", "TopUp", "Refund", "Manual"
        public string? ReferenceType { get; set; }

        // The ID of the Order/Subscription/etc that caused this
        public int? ReferenceId { get; set; }

        public DateTime CreatedAt { get; set; }

        public User User { get; set; } = null!;
    }
}
