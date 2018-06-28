namespace NServiceBus
{
    using System;
    using Extensibility;

    /// <summary>
    /// Allows routing a given message to remote sites, similar to Gateway.
    /// </summary>
    public static class SendToSiteExtensions
    {
        /// <summary>
        /// Instructs NServiceBus to send a given message to remote site(s).
        /// </summary>
        public static void SendToSites(this SendOptions options, params string[] sites)
        {
            if (sites.Length == 0)
            {
                throw new Exception("When sending to sites at least one site name has to be specified.");
            }
            var state = options.GetExtensions().GetOrCreate<ForwardSiteMessagesToRouterBehavior.State>();
            state.Sites = sites;
        }
    }
}