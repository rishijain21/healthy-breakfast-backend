using System;

namespace Sovva.Application.Exceptions
{
    /// <summary>
    /// Thrown when attempting to mark an order as prepared that is already prepared.
    /// Replaces string-matching on InvalidOperationException.Message.
    /// </summary>
    public class OrderAlreadyPreparedException : Exception
    {
        public int OrderId { get; }

        public OrderAlreadyPreparedException(int orderId)
            : base($"Order #{orderId} is already marked as prepared")
        {
            OrderId = orderId;
        }
    }
}
