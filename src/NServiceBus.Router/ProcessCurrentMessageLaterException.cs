namespace NServiceBus.Router
{
    using System;

    /// <summary>
    /// An exception that informs the recoverability policy to not bump the failure counters preventing the message from eventually moving to the poison queue.
    /// </summary>
    public class ProcessCurrentMessageLaterException : Exception
    {
        /// <summary>
        /// Creates a new instance.
        /// </summary>
        public ProcessCurrentMessageLaterException(string reason) : base(reason)
        {
        }
    }
}