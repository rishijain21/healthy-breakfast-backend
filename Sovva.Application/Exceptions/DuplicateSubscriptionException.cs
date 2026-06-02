using System;

namespace Sovva.Application.Exceptions
{
    public class DuplicateSubscriptionException : Exception
    {
        public DuplicateSubscriptionException()
            : base("An active subscription for this meal already exists.") { }
    }
}
