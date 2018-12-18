namespace NServiceBus.Router
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Linq;

    /// <summary>
    /// Configures sequence-based deduplication.
    /// </summary>
    public class DeduplicationSettings
    {
        internal bool RunInstaller;
        internal bool Uninstall;
        internal Dictionary<string, LinkSettings> Links = new Dictionary<string, LinkSettings>();
        internal void EnableLink(string sqlInterface, string linkInterface, string address, Func<SqlConnection> connFactory, int epochSize)
        {
            Links[address] = new LinkSettings(sqlInterface, linkInterface, connFactory, epochSize);
        }

        /// <summary>
        /// Enables installer to create database structure on startup.
        /// </summary>
        public void EnableInstaller()
        {
            RunInstaller = true;
        }

        /// <summary>
        /// Enables installer to create database structure on startup.
        /// </summary>
        [Obsolete("Do not use this method for anything other than tests. It will delete your data.")]
        public void EnableInstaller(bool uninstall)
        {
            RunInstaller = true;
            Uninstall = uninstall;
        }

        internal bool IsDeduplicationEnabledFor(string linkInterface)
        {
            return Links.Values.Any(x => x.LinkInterface == linkInterface);
        }

        internal bool IsDeduplicationEnabledFor(string linkInterface, string destinationAddress)
        {
            return Links.TryGetValue(destinationAddress, out var link) 
                   && link.LinkInterface == linkInterface;
        }

        internal bool IsDeduplicationEnabledFor(string sqlInterface, string linkInterface, string routerAddress)
        {
            return Links.TryGetValue(routerAddress, out var link)
                   && link.LinkInterface == linkInterface
                   && link.SqlInterface == sqlInterface;
        }

        internal string GetDestinationInterface(string destinationAddress)
        {
            return Links[destinationAddress].LinkInterface;
        }
    }
}