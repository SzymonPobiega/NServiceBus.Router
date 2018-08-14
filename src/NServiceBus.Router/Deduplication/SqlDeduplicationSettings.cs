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

        /// <summary>
        /// Configures destination for which the total order sequence should be generated.
        /// </summary>
        public void EnsureTotalOrderOfOutgoingMessages(string outgoingInterface, string destinationAddress)
        {
            outboxInterfaceToDestinationMap[destinationAddress] = outgoingInterface;
        }

        /// <summary>
        /// Sets the epoch size.
        /// </summary>
        public void EpochSize(int epochSize)
        {
            EpochSizeValue = epochSize;
        }

        //public void DecuplicateIncomingMessagesBasedOnTotalOrder(string incomingInterface, string originLogicalName)
        //{

        //}

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

        internal string GetInterface(string destinationAddress)
        {
            return outboxInterfaceToDestinationMap[destinationAddress];
        }

        internal IEnumerable<string> GetAllDestinations()
        {
            return outboxInterfaceToDestinationMap.Keys;
        }
    }
}