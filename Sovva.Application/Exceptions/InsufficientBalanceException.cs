using System;

namespace Sovva.Application.Exceptions
{
    public class InsufficientBalanceException : Exception
    {
        public decimal Required { get; }
        public decimal Available { get; }

        public InsufficientBalanceException(decimal required, decimal available)
            : base($"Insufficient balance. Required: {required}, Available: {available}")
        {
            Required = required;
            Available = available;
        }
    }
}
