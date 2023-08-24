using System;
using System.Threading;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Logging;
using NServiceBus.Raw;
using NServiceBus.Router;
using NServiceBus.Routing;
using NServiceBus.Transport;

delegate Task<ErrorHandleResult> PoisonMessageHandling(IErrorHandlingPolicyContext handlingContext, IMessageDispatcher dispatcher);

class ThrottlingRawEndpointConfig : IStartableRawEndpoint, IReceivingRawEndpoint
{
    public ThrottlingRawEndpointConfig(string queue, string poisonMessageQueueName, TransportDefinition transport, 
        Func<MessageContext, IMessageDispatcher, CancellationToken, Task> onMessage, PoisonMessageHandling poisonMessageHandling, 
        int? maximumConcurrency, int immediateRetries, int delayedRetries, int circuitBreakerThreshold, bool autoCreateQueue)
    {
        if (immediateRetries < 0)
        {
            throw new ArgumentException("Immediate retries count must not be less than zero.", nameof(immediateRetries));
        }
        if (delayedRetries < 0)
        {
            throw new ArgumentException("Delayed retries count must not be less than zero.", nameof(delayedRetries));
        }
        if (circuitBreakerThreshold < 0)
        {
            throw new ArgumentException("Circuit breaker threshold must not be less than zero.", nameof(circuitBreakerThreshold));
        }

        if (transport.TransportTransactionMode == TransportTransactionMode.SendsAtomicWithReceive)
        {
            throw new Exception("Router cannot operate in SendsAtomicWithReceive transaction mode. Please select other transaction mode.");
        }
        config = PrepareConfig(queue, poisonMessageQueueName, transport, onMessage, poisonMessageHandling, maximumConcurrency, immediateRetries, delayedRetries, circuitBreakerThreshold, autoCreateQueue);
    }

    RawEndpointConfiguration PrepareConfig(string inputQueue, string poisonMessageQueueName, TransportDefinition transport,
        Func<MessageContext, IMessageDispatcher, CancellationToken, Task> onMessage, PoisonMessageHandling poisonMessageHandling, int? maximumConcurrency, 
        int immediateRetries, int delayedRetries, int circuitBreakerThreshold, bool autoCreateQueue)
    {
        var circuitBreaker = new RepeatedFailuresCircuitBreaker(inputQueue, circuitBreakerThreshold, e =>
        {
            logger.Error($"Persistent error while processing messages in {inputQueue}. Entering throttled mode.", e);
            Task.Run(async () =>
            {
                await transitionSemaphore.WaitAsync().ConfigureAwait(false);
                try
                {
                    var oldEndpoint = endpoint;
                    var throttledConfig = PrepareThrottledConfig(inputQueue, poisonMessageQueueName, transport, onMessage, poisonMessageHandling, maximumConcurrency, immediateRetries, circuitBreakerThreshold, delayedRetries);
                    var newEndpoint = await RawEndpoint.Start(throttledConfig).ConfigureAwait(false);
                    endpoint = newEndpoint;
                    await oldEndpoint.Stop().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    logger.Error("Error when entering throttled mode", ex);
                }
                finally
                {
                    transitionSemaphore.Release();
                }
            });
        });
        var regularConfig = RawEndpointConfiguration.Create(inputQueue, transport, async (context, dispatcher, token) =>
        {
            await onMessage(context, dispatcher, token).ConfigureAwait(false);
            circuitBreaker.Success();
        }, poisonMessageQueueName);
        regularConfig.CustomErrorHandlingPolicy(new RegularModePolicy(inputQueue, circuitBreaker, poisonMessageHandling, immediateRetries, delayedRetries));

        Task onCriticalError(ICriticalErrorContext context, CancellationToken token)
        {
            logger.Fatal($"The receiver for queue {inputQueue} has encountered a severe error that is likely related to the connectivity with the broker or the broker itself.");
            return Task.CompletedTask;
        }

        regularConfig.CriticalErrorAction(onCriticalError);

        if (autoCreateQueue)
        {
            regularConfig.AutoCreateQueues();
        }
        if (maximumConcurrency.HasValue)
        {
            regularConfig.LimitMessageProcessingConcurrencyTo(maximumConcurrency.Value);
        }
        return regularConfig;
    }

    RawEndpointConfiguration PrepareThrottledConfig(string inputQueue, string poisonMessageQueueName, TransportDefinition transport, 
        Func<MessageContext, IMessageDispatcher, CancellationToken, Task> onMessage, PoisonMessageHandling poisonMessageHandling, int? maximumConcurrency, 
        int immediateRetries, int delayedRetries, int circuitBreakerThreshold)
    {
        var switchedBack = false;
        var throttledConfig = RawEndpointConfiguration.Create(inputQueue, transport, async (context, dispatcher, token) =>
        {
            await onMessage(context, dispatcher, token);
            if (switchedBack)
            {
                return;
            }
            await transitionSemaphore.WaitAsync().ConfigureAwait(false);
            Task.Run(async () =>
            {
                logger.Info("Exiting throttled mode.");
                try
                {
                    var oldEndpoint = endpoint;
                    var regularConfig = PrepareConfig(inputQueue, poisonMessageQueueName, transport, onMessage, poisonMessageHandling, maximumConcurrency, immediateRetries, delayedRetries, circuitBreakerThreshold, false);
                    var newEndpoint = await RawEndpoint.Start(regularConfig).ConfigureAwait(false);
                    endpoint = newEndpoint;
                    await oldEndpoint.Stop().ConfigureAwait(false);
                }
                catch (Exception e)
                {
                    logger.Error("Error when exiting throttled mode", e);
                }
                finally
                {
                    transitionSemaphore.Release();
                }
            }).Ignore();
            switchedBack = true;
        }, poisonMessageQueueName);

        throttledConfig.CustomErrorHandlingPolicy(new ThrottledModePolicy(inputQueue, immediateRetries));

        Task onCriticalError(ICriticalErrorContext context, CancellationToken token)
        {
            logger.Fatal($"The receiver for queue {inputQueue} has encountered a severe error that is likely related to the connectivity with the broker or the broker itself.");
            return Task.CompletedTask;
        }

        throttledConfig.CriticalErrorAction(onCriticalError);

        throttledConfig.LimitMessageProcessingConcurrencyTo(1);
        return throttledConfig;
    }
    
    public async Task<IStartableRawEndpoint> Create()
    {
        startable = await RawEndpoint.Create(config);
        config = null;
        return this;
    }

    async Task<IReceivingRawEndpoint> IStartableRawEndpoint.Start(CancellationToken cancellationToken)
    {
        endpoint = await startable.Start(cancellationToken).ConfigureAwait(false);
        startable = null;
        return this;
    }

    async Task<IStoppableRawEndpoint> IReceivingRawEndpoint.StopReceiving(CancellationToken cancellationToken)
    {
        await transitionSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        if (endpoint != null)
        {
            stoppable = await endpoint.StopReceiving(cancellationToken).ConfigureAwait(false);
            endpoint = null;
        }
        return this;
    }

    async Task IStoppableRawEndpoint.Stop(CancellationToken cancellationToken)
    {
        if (stoppable != null)
        {
            await stoppable.Stop(cancellationToken).ConfigureAwait(false);
            stoppable = null;
        }
    }

    string IRawEndpoint.ToTransportAddress(QueueAddress queueAddress) => startable?.ToTransportAddress(queueAddress) ?? endpoint.ToTransportAddress(queueAddress);

    Task IMessageDispatcher.Dispatch(TransportOperations outgoingMessages, TransportTransaction transaction, CancellationToken cancellationToken)
    {
        return endpoint != null 
            ? endpoint.Dispatch(outgoingMessages, transaction, cancellationToken) 
            : startable.Dispatch(outgoingMessages, transaction, cancellationToken);
    }

    string IRawEndpoint.TransportAddress => startable?.TransportAddress ?? endpoint.TransportAddress;
    string IRawEndpoint.EndpointName => startable?.EndpointName ?? endpoint.EndpointName;
    public ISubscriptionManager SubscriptionManager
    {
        get
        {
            return endpoint?.SubscriptionManager ?? startable?.SubscriptionManager;
        }
    }

    RawEndpointConfiguration config;
    IReceivingRawEndpoint endpoint;
    IStartableRawEndpoint startable;
    SemaphoreSlim transitionSemaphore = new SemaphoreSlim(1);

    static ILog logger = LogManager.GetLogger(typeof(ThrottlingRawEndpointConfig));
    IStoppableRawEndpoint stoppable;

    class RegularModePolicy : IErrorHandlingPolicy
    {
        RepeatedFailuresCircuitBreaker circuitBreaker;
        string inputQueue;
        PoisonMessageHandling poisonMessageHandling;
        int immediateRetries;
        int delayedRetries;

        public RegularModePolicy(string inputQueue, RepeatedFailuresCircuitBreaker circuitBreaker, PoisonMessageHandling poisonMessageHandling, int immediateRetries, int delayedRetries)
        {
            this.circuitBreaker = circuitBreaker;
            this.inputQueue = inputQueue;
            this.poisonMessageHandling = poisonMessageHandling;
            this.immediateRetries = immediateRetries;
            this.delayedRetries = delayedRetries;
        }

        public async Task<ErrorHandleResult> OnError(IErrorHandlingPolicyContext handlingContext, IMessageDispatcher dispatcher, CancellationToken cancellationToken = default)
        {
            if (handlingContext.Error.Exception is UnforwardableMessageException)
            {
                //UnforwardableMessageException immediately triggers poison queue
                return await poisonMessageHandling(handlingContext, dispatcher).ConfigureAwait(false);
            }
            if (handlingContext.Error.ImmediateProcessingFailures < immediateRetries)
            {
                return ErrorHandleResult.RetryRequired;
            }

            if (handlingContext.Error.DelayedDeliveriesPerformed >= delayedRetries)
            {
                //More than five times this message triggered throttled mode -> poison
                return await poisonMessageHandling(handlingContext, dispatcher).ConfigureAwait(false);
            }

            //Move to back of the queue.
            var incomingMessage = handlingContext.Error.Message;
            var message = new OutgoingMessage(incomingMessage.MessageId, incomingMessage.Headers, incomingMessage.Body);
            var operation = new TransportOperation(message, new UnicastAddressTag(inputQueue));

            //Only increment the delayed retries count if CB was not armed. That means that at least one message was
            //successfully forwarded in between previous failure of this message and this failure.
            //This prevents prematurely exhausting delayed retries attempts without triggering the throttled mode
            if (!circuitBreaker.IsArmed && !(handlingContext.Error.Exception is ProcessCurrentMessageLaterException))
            {
                var newDelayedRetriesHeaderValue = handlingContext.Error.DelayedDeliveriesPerformed + 1;
                incomingMessage.Headers[Headers.DelayedRetries] = newDelayedRetriesHeaderValue.ToString();
            }
            await dispatcher.Dispatch(new TransportOperations(operation), handlingContext.Error.TransportTransaction, cancellationToken)
                .ConfigureAwait(false);

            //Notify the circuit breaker
            await circuitBreaker.Failure(handlingContext.Error.Exception).ConfigureAwait(false);

            return ErrorHandleResult.Handled;
        }
    }

    class ThrottledModePolicy : IErrorHandlingPolicy
    {
        string inputQueue;
        int immediateRetries;

        public ThrottledModePolicy(string inputQueue, int immediateRetries)
        {
            this.inputQueue = inputQueue;
            this.immediateRetries = immediateRetries;
        }

        public async Task<ErrorHandleResult> OnError(IErrorHandlingPolicyContext handlingContext, IMessageDispatcher dispatcher, CancellationToken cancellationToken = default)
        {
            await Task.Delay(1000);
            if (handlingContext.Error.ImmediateProcessingFailures < immediateRetries)
            {
                return ErrorHandleResult.RetryRequired;
            }

            logger.Error("Error processing a message. Continuing in throttled mode.", handlingContext.Error.Exception);

            //Move to back of the queue.
            var incomingMessage = handlingContext.Error.Message;
            var message = new OutgoingMessage(incomingMessage.MessageId, incomingMessage.Headers, incomingMessage.Body);
            var operation = new TransportOperation(message, new UnicastAddressTag(inputQueue));

            await dispatcher.Dispatch(new TransportOperations(operation), handlingContext.Error.TransportTransaction, cancellationToken)
                .ConfigureAwait(false);

            return ErrorHandleResult.Handled;
        }
    }
}