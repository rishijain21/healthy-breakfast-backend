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
        
        // Navigation properties for display
        public string UserName { get; set; } = null!;
        public string UserEmail { get; set; } = null!;
    }
}
