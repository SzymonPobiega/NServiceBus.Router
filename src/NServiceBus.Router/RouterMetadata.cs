namespace NServiceBus.Router
{
    using System.Collections.Generic;

    /// <summary>
    /// Describes the router.
    /// </summary>
    public class RouterMetadata
    {
        /// <summary>
        /// Creates new router metadata object.
        /// </summary>
        public RouterMetadata(string name, List<string> interfaces)
        {
            Name = name;
            Interfaces = interfaces;
        }

        /// <summary>
        /// The name of the router.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// The list of router's interfaces.
        /// </summary>
        public IReadOnlyCollection<string> Interfaces { get; }
    }
}