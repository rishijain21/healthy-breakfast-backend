using System;

namespace Sovva.Application.DTOs
{
    public class ProcessOrdersResponseDto
    {
        public bool Success { get; set; }
        public string? Message { get; set; }
        public DateTime DeliveryDate { get; set; }
        public int OrdersFound { get; set; }
        public int OrdersPending { get; set; }
        public int OrdersAlreadyConfirmed { get; set; }
        public int OrdersConfirmed { get; set; }
        public int OrdersFailed { get; set; }
        public DateTime Timestamp { get; set; }
        public string? Note { get; set; }
    }
}
