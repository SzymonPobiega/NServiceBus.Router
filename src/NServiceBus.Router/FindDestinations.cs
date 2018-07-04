
namespace NServiceBus.Router
{
    using System.Collections.Generic;
    using Transport;

    /// <summary>
    /// Defines contract for looking up destinations based on the message.
    /// </summary>
    /// <param name="message">Incoming message.</param>
    /// <returns>List of destinations.</returns>
    public delegate IEnumerable<Destination> FindDestinations(MessageContext message);
}
