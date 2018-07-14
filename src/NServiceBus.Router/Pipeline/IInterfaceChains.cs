namespace NServiceBus.Router
{
    /// <summary>
    /// Allows accessing chains for a given interface.
    /// </summary>
    public interface IInterfaceChains
    {
        /// <summary>
        /// Returns a chains collection associated with interface <paramref name="interface"/>
        /// </summary>
        IChains GetChainsFor(string @interface);
    }
}