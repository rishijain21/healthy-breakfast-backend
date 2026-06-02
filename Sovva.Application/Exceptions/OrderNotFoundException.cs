using System;

namespace Sovva.Application.Exceptions
{
    /// <summary>
    /// Thrown when an order is not found during kitchen operations.
    /// Replaces string-matching on InvalidOperationException.Message.
    /// </summary>
    public class OrderNotFoundException : Exception
    {
        public int OrderId { get; }

        public OrderNotFoundException(int orderId)
            : base($"Order #{orderId} not found")
        {
            OrderId = orderId;
        }
    }
}
