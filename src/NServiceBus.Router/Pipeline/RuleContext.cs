﻿namespace NServiceBus.Router
{
    using System;
    using System.Threading;
    using Extensibility;

    /// <summary>
    /// Base class for all rule contexts.
    /// </summary>
    public abstract class RuleContext : ContextBag, IRuleContext
    {
        /// <summary>
        /// Creates new instance.
        /// </summary>
        protected RuleContext(RuleContext parentContext, string @interface = null) 
            : base(parentContext.Extensions)
        {
            Interface = @interface ?? parentContext.Interface ?? throw new Exception("Interface is required.");
            CancellationToken = parentContext.CancellationToken;
        }

        /// <summary>
        /// Creates new instance.
        /// </summary>
        protected internal RuleContext(RootContext parentContext, string @interface)
            : base(parentContext?.Extensions)
        {
            Interface = @interface;
            CancellationToken = parentContext.CancellationToken;
        }

        /// <summary>
        /// Interface to which the chain that contains this rule belongs.
        /// </summary>
        public string Interface { get; }

        /// <summary>
        /// The collection of all chains associated with the interface which contains this chain.
        /// </summary>
        public IChains Chains => Extensions.Get<IInterfaceChains>().GetChainsFor(Interface);

        /// <summary>
        /// Allows extending the rule context by adding arbitrary values.
        /// </summary>
        public ContextBag Extensions => this;

        /// <summary>
        /// Cancellation token for the message processing.
        /// </summary>
        public CancellationToken CancellationToken { get; }
    }
}