using System;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Router;
using NServiceBus.Configuration.AdvancedExtensibility;
using NServiceBus.Logging;
using NServiceBus.Raw;
using NServiceBus.Settings;
using NServiceBus.Transport;

interface Interface
{
    string Name { get; }
    Task Forward(string source, MessageContext context);
    Task Initialize(Func<MessageContext, Task> onMessage);
    Task StartReceiving();
    Task StopReceiving();
    Task Stop();
}

class Interface<T> : Interface where T : TransportDefinition, new()
{
    public string Name { get; }
    public Interface(string endpointName, string interfaceName, Action<TransportExtensions<T>> transportCustomization, ForwardingConfiguration forwardingConfiguration, Func<RouteTable> routeTable, string poisonQueue, int? maximumConcurrency, InterceptMessageForwarding interceptMethod, bool autoCreateQueues, string autoCreateQueuesIdentity, int immediateRetries, int delayedRetries, int circuitBreakerThreshold)
    {
        this.endpointName = endpointName;
        this.forwardingConfiguration = forwardingConfiguration;
        this.interceptMethod = interceptMethod;
        this.routeTable = routeTable;
        Name = interfaceName;
        sendForwarder = forwardingConfiguration.PrepareSending();
        replyForwarder = new ReplyForwarder();

        rawConfig = new ThrottlingRawEndpointConfig<T>(endpointName, poisonQueue, ext =>
            {
                SetTransportSpecificFlags(ext.GetSettings(), poisonQueue);
                transportCustomization?.Invoke(ext);
            },
            async (context, _) =>
            {
                var intent = GetMesssageIntent(context);
                if (intent == MessageIntentEnum.Subscribe || intent == MessageIntentEnum.Unsubscribe)
                {
                    await subscriptionReceiver.Receive(context, intent);
                }
                await onMessage(context);
            },
            (context, dispatcher) =>
            {
                log.Error("Moving poison message to the error queue", context.Error.Exception);
                return context.MoveToErrorQueue(poisonQueue);
            },
            maximumConcurrency,
            immediateRetries, delayedRetries, circuitBreakerThreshold, autoCreateQueues, autoCreateQueuesIdentity);
    }

    static void SetTransportSpecificFlags(SettingsHolder settings, string poisonQueue)
    {
        settings.Set("errorQueue", poisonQueue);
        settings.Set("RabbitMQ.RoutingTopologySupportsDelayedDelivery", true);
    }

    public Task Forward(string source, MessageContext context)
    {
        return interceptMethod(source, context, sender.Dispatch, 
            dispatch => Forward(source, context, new InterceptingDispatcher(sender, dispatch, endpointName)));
    }

    Task Forward(string incomingInterface, MessageContext context, IRawEndpoint dispatcher)
    {
        var intent = GetMesssageIntent(context);

        switch (intent)
        {
            case MessageIntentEnum.Subscribe:
            case MessageIntentEnum.Unsubscribe:
                return subscriptionForwarder.Forward(incomingInterface, context, intent, dispatcher, routeTable());
            case MessageIntentEnum.Publish:
                return publishForwarder.Forward(context, dispatcher);
            case MessageIntentEnum.Send:
                return sendForwarder.Forward(incomingInterface, context, dispatcher, routeTable());
            case MessageIntentEnum.Reply:
                return replyForwarder.Forward(context, intent, dispatcher);
            default:
                throw new UnforwardableMessageException("Unroutable message intent: " + intent);
        }
    }

    static MessageIntentEnum GetMesssageIntent(MessageContext message)
    {
        var messageIntent = default(MessageIntentEnum);
        if (message.Headers.TryGetValue(Headers.MessageIntent, out var messageIntentString))
        {
            Enum.TryParse(messageIntentString, true, out messageIntent);
        }
        return messageIntent;
    }

    public async Task Initialize(Func<MessageContext, Task> onMessage)
    {
        this.onMessage = onMessage;
        sender = await rawConfig.Create().ConfigureAwait(false);
        forwardingConfiguration.PreparePubSub(sender, out publishForwarder, out subscriptionReceiver, out subscriptionForwarder);
    }

    public async Task StartReceiving()
    {
        receiver = await sender.Start().ConfigureAwait(false);
    }

    public async Task StopReceiving()
    {
        if (receiver != null)
        {
            stoppable = await receiver.StopReceiving().ConfigureAwait(false);
        }
        else
        {
            stoppable = null;
        }
    }

    public async Task Stop()
    {
        if (stoppable != null)
        {
            await stoppable.Stop().ConfigureAwait(false);
            stoppable = null;
        }
    }

    static ILog log = LogManager.GetLogger(typeof(Interface));
    string endpointName;
    ForwardingConfiguration forwardingConfiguration;
    InterceptMessageForwarding interceptMethod;
    Func<RouteTable> routeTable;
    Func<MessageContext, Task> onMessage;
    IReceivingRawEndpoint receiver;
    IStartableRawEndpoint sender;
    IStoppableRawEndpoint stoppable;

    SubscriptionReceiver subscriptionReceiver;
    SubscriptionForwarder subscriptionForwarder;
    IPublishForwarder publishForwarder;

    ThrottlingRawEndpointConfig<T> rawConfig;
    SendForwarder sendForwarder;
    ReplyForwarder replyForwarder;
}
