### Symbol definitions

- `->` send message directly via a queue
- `->>` pass message in-memory
- `+>` subscribe natively
- `=>` publish natively 

## Migrating subscriber first

### Before migration

Endpoints:
 
 - Subscriber (MSMQ)
 - Publisher (MSMQ)

Actions:

 - Subscriber -> Subscribe -> Publisher
 - Publisher -> Event -> Subscriber

### Subscriber is migrated

The subscriber is migrated to the SQL Server transport. Once the binaries are upgraded and the endpoint is restarted, it re-subscribes to all events is processes. Instead of

```
var routing = c.UseTransport<MsmqTransport>().Routing();
routing.RegisterPublisher(typeof(MyEvent), "Publisher");
```

the subscribe messages are now routed based on the migration mode configuration:

```
var routing = c.EnableTransportMigration<MsmqTransport, SqlServerTransport>(to => {}, tn => {});
routing.RegisterPublisher(typeof(MyEvent), "Publisher");
```

Endpoints:

 - Subscriber (SQL)
   - Subscriber_Migrator (SQL)
   - Subscriber (Shadow) (MSMQ)
 - Publisher (MSMQ)

Actions:

 - Subscriber +> Event
 - Subscriber -> Subscribe -> Subscriber_Migrator
 - Subscriber_Migrator ->> Subscribe ->> Subscriber (Shadow)
 - Subscriber (Shadow) -> Subscribe -> Publisher
 - Publisher -> Event -> Subscriber (Shadow)
 - Subscriber (Shadow) ->> Event ->> Subscriber_Migrator
 - Subscriber_Migrator -> Event -> Subscriber

### Publisher is migrated

The Publisher is migrated and restarted. Subscriber continues to run. The publisher uses dual publish mode in which it both publishes using native mechanism and based on the subscription store. The events published natively carry a special headers that lists all the destinations that are going to received a given event in both (native and storage-driven) ways.

Endpoints:

 - Subscriber (SQL)
   - Subscriber_Migrator (SQL)
   - Subscriber (Shadow) (MSMQ)
 - Publisher (SQL)
   - Publisher_Migrator (SQL)
   - Publisher (Shadow) (MSMQ)

Actions:

 - Publisher -> Event -> Subscriber (Shadow)
 - Subscriber (Shadow) => Subscriber_Migrator
 - Subscriber_Migrator -> Subscriber
 - Publisher => Event => Subscriber

As a result, Subscriber gets two copies of the event. However, based on the mentioned header is able to discard the copy published natively.

### Subscriber is restarted

After some period of time the subscriber is restarted. After being restarted is re-subscribes to all events it processes.

Endpoints:

 - Subscriber (SQL)
   - Subscriber_Migrator (SQL)
   - Subscriber (Shadow) (MSMQ)
 - Publisher (SQL)
   - Publisher_Migrator (SQL)
   - Publisher (Shadow) (MSMQ)

Actions:

 - Subscriber +> Event
 - Subscriber -> Subscribe -> Subscriber_Migrator
 - Subscriber_Migrator ->> Subscribe ->> Subscriber (Shadow)
 - Subscriber (Shadow) -> Subscribe -> Publisher (Shadow)
 - Publisher (Shadow) ->> Subscribe ->> Publisher_Migrator
 - Publisher_Migrator -> Unsubscribe -> Publisher
 - Publisher => Event => Subscriber

The shadow endpoint adds a special header when it forwards the Subscribe message. Based on that header the Publisher_Migrator endpoint can detect that the Subscribe message want via a shadow endpoint. That means that both the subscriber and the publisher have been migrated and there is no need to continue routing messages via MSMQ transport. The Migrator turns the Subscribe message into an Unsubscribe message.

As a result, the Publisher no longer has any subscription storage entries for that event and only publishes it via native mechanism.

### Migration mode disabled

Once all endpoints have been migrated and restarted, the migration mode can be disabled. This can be done when the subscription storage no longer contains entries for the events exchanged by the Publisher and the Subscriber and there is no messages in the MSMQ shadow queues.

Endpoints:

 - Subscriber (SQL)
 - Publisher (SQL)

Actions:

 - Subscriber +> Event
 - Publisher => Event => Subscriber


## Migrating publisher first

### Before migration

Endpoints:
 
 - Subscriber (MSMQ)
 - Publisher (MSMQ)

Actions:

 - Subscriber -> Subscribe -> Publisher
 - Publisher -> Event -> Subscriber

### Publisher is migrated

The subscriber is migrated to the SQL Server transport. The subscriber continues to run.

Endpoints:

 - Subscriber (MSMQ)
 - Publisher (SQL)
   - Publisher_Migrator (SQL)
   - Publisher (Shadow) (MSMQ)

Actions:

 - Publisher -> Event -> Publisher_Migrator
 - Publisher_Migrator ->> Event ->> Publisher (Shadow)
 - Publisher (Shadow) -> Event -> Subscriber

The event is sent directly (not published) from the Migrator to the Publisher endpoints.

### Subscriber is restarted

After some period of time the subscriber is restarted. After being restarted is re-subscribes to all events it processes.

Endpoints:

 - Subscriber (MSMQ)
 - Publisher (SQL)
   - Publisher_Migrator (SQL)
   - Publisher (Shadow) (MSMQ)

Actions:

 - Subscriber -> Subscribe -> Publisher (Shadow)
 - Publisher (Shadow) ->> Subscribe ->> Publisher_Migrator
 - Publisher_Migrator -> Subscribe -> Publisher
 - Publisher => Event => 
 - Publisher -> Event -> Publisher_Migrator
 - Publisher_Migrator ->> Event ->> Publisher (Shadow)
 - Publisher (Shadow) -> Event -> Subscriber

The subscription process works the same as when both endpoints used MSMQ as a transport. The only difference is that the Subscribe message now travels via the Shadow and Migrator endpoints. The Publisher publishes the Event both natively and using subscription storage but there is no native subscribers (yet).

### Subscriber is migrated

The subscriber is migrated to the SQL Server transport. Once the binaries are upgraded and the endpoint is restarted, it re-subscribes to all events is processes. Instead of

```
var routing = c.UseTransport<MsmqTransport>().Routing();
routing.RegisterPublisher(typeof(MyEvent), "Publisher");
```

the subscribe messages are now routed based on the migration mode configuration:

```
var routing = c.EnableTransportMigration<MsmqTransport, SqlServerTransport>(to => {}, tn => {});
routing.RegisterPublisher(typeof(MyEvent), "Publisher");
```

Endpoints:

 - Subscriber (SQL)
   - Subscriber_Migrator (SQL)
   - Subscriber (Shadow) (MSMQ)
 - Publisher (SQL)
   - Publisher_Migrator (SQL)
   - Publisher (Shadow) (MSMQ)

Actions:

 - Subscriber +> Event
 - Subscriber -> Subscribe -> Subscriber_Migrator
 - Publisher => Event => Subscriber
 - Publisher -> Event -> Publisher_Migrator
 - Publisher_Migrator ->> Event ->> Publisher (Shadow)
 - Publisher (Shadow) -> Event -> Subscriber
 - Subscriber_Migrator ->> Subscribe ->> Subscriber (Shadow)
 - Subscriber (Shadow) -> Subscribe -> Publisher (Shadow)
 - Publisher (Shadow) ->> Subscribe ->> Publisher_Migrator
 - Publisher_Migrator -> Unsubscribe -> Publisher

The Subscriber subscribes natively and via message-driven mechanism. After the native subscription is added but before the Subscribe message reaches its destination the Publisher publishes an event. That event is dual-published via native and storage-driven mechanism. That results in two copies of the event being delivered to tht Subscriber. However, due to a special header the Subscriber is able to discard one of the copies.

The Subscribe message is routed via two Shadow endpoints. The Migrator detects that and turns it into an Unsubscribe message.

### Migration mode disabled

Once all endpoints have been migrated and restarted, the migration mode can be disabled. This can be done when the subscription storage no longer contains entries for the events exchanged by the Publisher and the Subscriber and there is no messages in the MSMQ shadow queues.

Endpoints:

 - Subscriber (SQL)
 - Publisher (SQL)

Actions:

 - Subscriber +> Event
 - Publisher => Event => Subscriber

## Migrating sender and receiver

### Before migration

Endpoints:
 
 - Sender (MSMQ)
 - Sender (MSMQ)

Actions:

 - Sender -> Message -> Receiver
 - Receiver -> Reply -> Sender

### Sender is migrated

The sender is migrated to the SQL Server transport. The receiver remains on MSMQ. The routing configuration is moved to the migration mode config

```
var routing = c.UseTransport<MsmqTransport>().Routing();
routing.RouteToEndpoint(typeof(MyMessage), "Receiver");
```

the subscribe messages are now routed based on the migration mode configuration:

```
var routing = c.EnableTransportMigration<MsmqTransport, SqlServerTransport>(to => {}, tn => {});
routing.RouteToEndpoint(typeof(MyMessage), "Receiver");
```

Endpoints:
 
 - Sender (SQL)
   - Sender_Migrator (SQL)
   - Sender (Shadow) (MSMQ)
 - Receiver (MSMQ)

Actions:

 - Sender -> Message -> Sender_Migrator
 - Sender_Migrator ->> Message ->> Sender (Shadow)
 - Sender (Shadow) -> Message -> Receiver
 - Receiver -> Reply -> Sender (Shadow)
 - Sender (Shadow) ->> Reply ->> Sender_Migrator
 - Sender_Migrator -> Reply -> Sender

The Reply message is routed based on the standard Router's breadcrumb-based reply routing mechanism

### Receiver is migrated

The receiver is migrated to SQL Server transport. The sender remains on MSMQ.

Endpoints:
 
 - Sender (MSMQ)
 - Receiver (SQL)
   - Receiver_Migrator (SQL)
   - Receiver (Shadow) (MSMQ)

Actions:
 - Sender -> Message -> Receiver (Shadow)
 - Receiver (Shadow) ->> Message ->> Receiver_Migrator
 - Receiver_Migrator -> Message -> Receiver
 - Receiver -> Reply -> Receiver_Migrator
 - Receiver_Migrator ->> Reply ->> Receiver (Shadow)
 - Receiver (Shadow) -> Reply -> Sender

### Sender and Receiver are migrated

In this mode both message and the reply travel through both sender's and receiver's routers.

Endpoints:
 
 - Sender (SQL)
   - Sender_Migrator (SQL)
   - Sender (Shadow) (MSMQ)
 - Receiver (SQL)
   - Receiver_Migrator (SQL)
   - Receiver (Shadow) (MSMQ)

Actions:

 - Sender -> Message -> Sender_Migrator
 - Sender_Migrator ->> Message ->> Sender (Shadow)
 - Sender (Shadow) -> Message -> Receiver (Shadow)
 - Receiver (Shadow) ->> Message ->> Receiver_Migrator
 - Receiver_Migrator -> Message -> Receiver

 - Receiver -> Reply -> Receiver_Migrator
 - Receiver_Migrator ->> Reply ->> Receiver (Shadow)
 - Receiver (Shadow) -> Reply -> Sender (Shadow)
 - Sender (Shadow) ->> Reply ->> Sender_Migrator
 - Sender_Migrator -> Reply -> Sender