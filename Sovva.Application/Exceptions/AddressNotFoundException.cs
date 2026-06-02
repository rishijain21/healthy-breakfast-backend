using System;

namespace Sovva.Application.Exceptions
{
    public class AddressNotFoundException : Exception
    {
        public AddressNotFoundException(long userId)
            : base($"No delivery address found for user {userId}") { }

        public AddressNotFoundException(long userId, string message)
            : base(message) { }
    }
}
