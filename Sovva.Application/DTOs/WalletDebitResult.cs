namespace Sovva.Application.DTOs
{
    public class WalletDebitResult
    {
        public bool Success { get; set; }
        public decimal NewBalance { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}