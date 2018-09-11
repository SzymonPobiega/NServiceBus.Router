namespace NServiceBus.Router.Deduplication
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Linq;

    /// <summary>
    /// Configures sequence-based deduplication.
    /// </summary>
    public class SqlDeduplicationSettings
    {
        internal Func<SqlConnection> ConnFactory;
        internal int EpochSizeValue;
        Dictionary<string, string> outboxInterfaceToDestinationMap = new Dictionary<string, string>();
        Dictionary<string, string> inboxInterfaceToSourceMap = new Dictionary<string, string>();

        /// <summary>
        /// Configures destination router for which the total order sequence should be generated.
        /// </summary>
        public void EnsureTotalOrderOfOutgoingMessages(string outgoingInterface, string destinationRouter)
        {
            outboxInterfaceToDestinationMap[destinationRouter] = outgoingInterface;
        }

        /// <summary>
        /// Sets the epoch size.
        /// </summary>
        public void EpochSize(int epochSize)
        {
            EpochSizeValue = epochSize;
        }

        /// <summary>
        /// Configures the source router which ensures total order of messages to allow deduplication.
        /// </summary>
        public void DecuplicateIncomingMessagesBasedOnTotalOrder(string incomingInterface, string originRouter)
        {
            inboxInterfaceToSourceMap[originRouter] = incomingInterface;
        }

        /// <summary>
        /// Sets the connection factory.
        /// </summary>
        public void ConnectionFactory(Func<SqlConnection> connFactory)
        {
            ConnFactory = connFactory;
        }

        internal bool IsOutboxEnabledFor(string outgoingInterface)
        {
            return outboxInterfaceToDestinationMap.Values.Contains(outgoingInterface);
        }

        internal bool IsOutboxEnabledFor(string outgoingInterface, string destinationAddres)
        {
            return outboxInterfaceToDestinationMap.TryGetValue(destinationAddres, out var iface) 
                   && iface == outgoingInterface;
        }

        internal string GetDestinationInterface(string destinationAddress)
        {
            return outboxInterfaceToDestinationMap[destinationAddress];
        }

        internal IEnumerable<string> GetAllDestinations()
        {
            return outboxInterfaceToDestinationMap.Keys;
        }

        internal IEnumerable<string> GetAllSources()
        {
            return inboxInterfaceToSourceMap.Keys;
        }

        internal IEnumerable<string> GetAllInboxInterfaces()
        {
            return inboxInterfaceToSourceMap.Values;
        }

        internal bool IsInboxEnabledFor(string incomingInterface)
        {
            return inboxInterfaceToSourceMap.Values.Contains(incomingInterface);
        }

        internal bool IsInboxEnabledFor(string incomingInterface, string sourceEndpoint)
        {
            return inboxInterfaceToSourceMap.TryGetValue(sourceEndpoint, out var iface)
                   && iface == incomingInterface;
        }
    }
}