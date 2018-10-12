namespace NServiceBus.Router
{
    using System.Collections.Generic;

    /// <summary>
    /// Represents a set of headers associated with a received message.
    /// </summary>
    public interface IReceivedMessageHeaders : IReadOnlyDictionary<string, string>
    {
        /// <summary>
        /// Returns a copy of the header set.
        /// </summary>
        Dictionary<string, string> Copy();
    }
}