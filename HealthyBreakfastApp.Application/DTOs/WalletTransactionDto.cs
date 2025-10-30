namespace HealthyBreakfastApp.Application.DTOs
{
    public class WalletTransactionDto
    {
        public int TransactionId { get; set; }
        public int UserId { get; set; }
        public decimal Amount { get; set; }
        public string Type { get; set; } = null!;
        public string Description { get; set; } = null!;
        public DateTime CreatedAt { get; set; }

        // ✅ REMOVED: UserName and UserEmail properties (redundant data eliminated)
    }
}
