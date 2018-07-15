namespace NServiceBus.Router
{
    using System;

    /// <summary>
    /// Represents a route for a message.
    /// </summary>
    public class Route
    {
        /// <summary>
        /// Creates new instance.
        /// </summary>
        /// <param name="destination">The logical name of the immediate destination.</param>
        /// <param name="gateway">The gateway through which to send the message.</param>
        public Route(string destination, string gateway)
        {
            if (destination == null && gateway == null)
            {
                throw new ArgumentException("Either destination or gateway has to be specified.");
            }

            Destination = destination;
            Gateway = gateway;
        }

        /// <summary>
        /// The gateway through which to send the message.
        /// </summary>
        public string Gateway { get; }

        /// <summary>
        /// The logical name of the immediate destination.
        /// </summary>
        public string Destination { get; }
    }
}