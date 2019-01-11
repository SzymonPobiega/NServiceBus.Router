![Icon](https://raw.github.com/SzymonPobiega/NServiceBus.Router/master/icons/router.png)

# NServiceBus.Router

Cross-transport, cross-site and cross-cloud router component for NServiceBus

## Design

The NServiceBus Router, just like a normal networking router, consists of multiple interfaces and the interconnection between them. It has been modeled after the Linux [iptables](https://netfilter.org/projects/iptables/index.html) packet filtering tool. The following diagram shows a router with two interfaces

```
PUMP      PRE-ROUTING          FORWARDING           POST-ROUTING         DISPATCHER
+----+    +---+---+---+---+    +---+---+---+---+    +-------+---+---+    +----+
|    |    |   |   |   |   |    |   |   |   |   |    |   |   |   |   |    |    |
|    +--->|   |   |   |   +--->|   |   |   |   | +->|   |   |   |   +--->|    |
|    |    |   |   |   |   |    |   |   |   |   | |  |   |   |   |   |    |    |
+----+    +---+---+---+---+    +---+---+-+-+---+ |  +---+---+---+---+    +----+
                                         |       | 
                                     +-----------+
                                     |   |
                                     |   +-------+
                                     |           |
+----+    +---+---+---+---+    +---+-+-+---+---+ |  +---+---+---+---+    +----+
|    |    |   |   |   |   |    |   |   |   |   | |  |   |   |   |   |    |    |
|    +--->|   |   |   |   +--->|   |   |   |   | +->|   |   |   |   +--->|    |
|    |    |   |   |   |   |    |   |   |   |   |    |   |   |   |   |    |    |
+----+    +---+---+---+---+    +---+---+---+---+    +---+---+---+---+    +----+
```

Each interfaces contains two transport components, a pump and a dispatcher. These components are implemented in the NServiceBus transport packages e.g. [NServiceBus.SqlServer](https://www.nuget.org/packages/NServiceBus.SqlServer/). The pump is an active component (it manages and runs tasks/threads) while the pump is a passive one. The dispatcher receives messages from the transport and invokes the interface's `pre-routing` chain.

### Chains

All chains consist of rules. A rule is a small component that executes an action in the context of a message and forwards the invocation to subsequent rules. Rules are grouped into chains.

There are three major chain groups modeled after iptables: `pre-routing`, `forwarding` and `post-routing`. All received messages enter through the `pre-routing` chain. Within that chain they are categorized based on their [intent](https://docs.particular.net/nservicebus/messaging/headers#messaging-interaction-headers-nservicebus-messageintent) (i.e. `send`, `publish`, `reply`, `subscribe`). The `pre-routing` chain is responsible for extracting information from message headers and figuring out the destinations for the messages.

Next, messages are moved to a forwarding chain appropriate for their intent. These chains are responsible for moving messages between interfaces i.e. a forwarding chain rule of one interface moves the message to the `post-routing` chain of another interface.

Last but not least, the message enters the `post-routing` chain which prepares it to be sent out to the transport via the dispatcher component.

### Modules

Modules (implementations of `IModule`) are components inside a router that can have their own thread of execution independent of the pump. Modules are started when the router starts and can execute any arbitrary logic. Modules get access to the chains so they can send out messages via the `post-routing` chain. They can't receive messages.

### Features

Features (implementations of `IFeature`) are larger-scale components that group modules and rules.

### Intent

Router categorizes messages based on the [intent](https://docs.particular.net/nservicebus/messaging/headers#messaging-interaction-headers-nservicebus-messageintent):
 * Send
 * Publish
 * Reply
 * Subscribe and Unsubscribe

Each intent is associated with a different type of routing.

#### Send

Messages with `send` intent are routed using the routing protocol configured for the router.

#### Publish

Routing of `publish` messages depends on the transport. If the transport supports native Publish/Subscribe, the `publish` messages are passed directly to the transport. Otherwise the they are routed based on the subscription storage. The subscription storage entries are added/removed in the `pre-routing` stage of processing incoming `subscribe` and `unsubscribe` messages.

#### Reply

Messages with `reply` intent are routed back to the sender of an original message using breadcrubs contained in the message headers. `send` and `publish` routing leaves breadcrumbs at each router a message travels through. There breadcrubms then get copied to the reply by the endpoints that handles the `send` or `publish`.

#### Subscribe and Unsubscribe

These messages are routed using the routing protocol configured for the router. They are special messages that inform the recipient (be it regular endpoint or router) that the sender wants to get notified about evens of a given type. 


## Icon

[Router](https://thenounproject.com/search/?q=router&i=1602484) by [SBTS](https://thenounproject.com/sbts2018/) from the Noun Project

