namespace Sovva.Application.DTOs
{
    public class UserWalletSummaryDto
    {
        public int UserId { get; set; }
        public string UserName { get; set; } = null!;
        public string UserEmail { get; set; } = null!;
        public decimal CurrentBalance { get; set; }
        public decimal TotalCredits { get; set; }
        public decimal TotalDebits { get; set; }
        public int TransactionCount { get; set; }
        public DateTime LastTransactionDate { get; set; }
    }
}
