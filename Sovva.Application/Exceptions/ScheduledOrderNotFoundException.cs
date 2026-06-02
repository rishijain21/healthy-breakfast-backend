using System;

namespace Sovva.Application.Exceptions
{
    public class ScheduledOrderNotFoundException : Exception
    {
        public int ScheduledOrderId { get; }

        public ScheduledOrderNotFoundException(int scheduledOrderId)
            : base($"Scheduled order #{scheduledOrderId} not found.")
        {
            ScheduledOrderId = scheduledOrderId;
        }
    }
}
