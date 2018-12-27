namespace NServiceBus.Router
{
    using System.Threading.Tasks;

    /// <summary>
    /// Represents extension of the router.
    /// </summary>
    public interface IModule
    {
        /// <summary>
        /// Starts the module.
        /// </summary>
        Task Start(RootContext rootContext, SettingsHolder extensibilitySettings);

        /// <summary>
        /// Stops the module.
        /// </summary>
        /// <returns></returns>
        Task Stop();
    }
}