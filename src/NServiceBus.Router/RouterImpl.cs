﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NServiceBus;
using NServiceBus.Router;
using NServiceBus.Transport;

class RouterImpl : IRouter
{
    public RouterImpl(string name, Interface[] interfaces, IRoutingProtocol routingProtocol)
    {
        this.name = name;
        this.routingProtocol = routingProtocol;
        this.interfaces = interfaces.ToDictionary(x => x.Name, x => x);
    }

    public async Task Start()
    {
        await routingProtocol.Start(new RouterMetadata(name, interfaces.Keys.ToList())).ConfigureAwait(false);
        await Task.WhenAll(interfaces.Values.Select(p => p.Initialize(ctx => Forward(p.Name, ctx)))).ConfigureAwait(false);
        await Task.WhenAll(interfaces.Values.Select(p => p.StartReceiving())).ConfigureAwait(false);
    }

    Task Forward(string incomingIface, MessageContext msg)
    {
        DetectCycles(msg);
        var intent = GetMesssageIntent(msg);
        switch (intent)
        {
            case MessageIntentEnum.Subscribe:
            case MessageIntentEnum.Unsubscribe:
            case MessageIntentEnum.Send:
                var outgoingInterfaces = routingProtocol.RouteTable.GetOutgoingInterfaces(incomingIface, msg);
                var forwardTasks = outgoingInterfaces.Select(i => GetInterface(i).Forward(incomingIface, msg));
                return Task.WhenAll(forwardTasks.ToArray());
            case MessageIntentEnum.Publish:
                return Task.WhenAll(interfaces.Values.Where(p => p.Name != incomingIface).Select(x => x.Forward(incomingIface, msg)));
            case MessageIntentEnum.Reply:
                return GetInterface(InterfaceForReply(msg)).Forward(incomingIface, msg);
            default:
                throw new UnforwardableMessageException("Not supported message intent: " + intent);
        }
    }

    void DetectCycles(MessageContext msg)
    {
        if (msg.Headers.TryGetValue("NServiceBus.Bridge.Trace", out var trace))
        {
            trace.DecodeTLV((t, v) =>
            {
                if (t == "via" && v == name) //We forwarded this message
                {
                    throw new UnforwardableMessageException("Routing cycle detected: " + trace);
                }
            });
        }
    }

    Interface GetInterface(string interfaceName)
    {
        if (!interfaces.TryGetValue(interfaceName, out var iface))
        {
            throw new UnforwardableMessageException($"Interface '{interfaceName}' is not configured");
        }
        return iface;
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

    public async Task Stop()
    {
        await Task.WhenAll(interfaces.Values.Select(s => s.StopReceiving())).ConfigureAwait(false);
        await Task.WhenAll(interfaces.Values.Select(s => s.Stop())).ConfigureAwait(false);
        await routingProtocol.Stop().ConfigureAwait(false);
    }

    static string InterfaceForReply(MessageContext context)
    {
        string destinationIface = null;
        if (!context.Headers.TryGetValue(Headers.CorrelationId, out var correlationId))
        {
            throw new UnforwardableMessageException($"The reply has to contain a '{Headers.CorrelationId}' header set by the sending endpoint when sending out the initial message.");
        }
        correlationId.DecodeTLV((t, v) =>
        {
            if (t == "iface" || t == "port") //Port for compat reasons
            {
                destinationIface = v;
            }
        });

        if (destinationIface == null)
        {
            throw new UnforwardableMessageException("The reply message does not contain \'iface\' correlation parameter required to route the message.");
        }
        return destinationIface;
    }


    string name;
    IRoutingProtocol routingProtocol;
    Dictionary<string, Interface> interfaces;
}