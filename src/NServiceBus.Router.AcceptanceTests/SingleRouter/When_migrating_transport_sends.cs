namespace NServiceBus.Router.AcceptanceTests.SingleRouter
{
    using System;
    using System.Threading.Tasks;
    using AcceptanceTesting;
    using AcceptanceTesting.Customization;
    using Migrator;
    using NServiceBus.AcceptanceTests;
    using NServiceBus.AcceptanceTests.EndpointTemplates;
    using NUnit.Framework;

    [TestFixture]
    public class When_migrating_transport_sends : NServiceBusAcceptanceTest
    {
        static string SenderEndpointName => Conventions.EndpointNamingConvention(typeof(Sender));
        static string ReceiverEndpointName => Conventions.EndpointNamingConvention(typeof(Receiver));

        [Test]
        public async Task Should_not_lose_messages_before_migration()
        {
            var beforeMigration = await Scenario.Define<Context>()
                .WithEndpoint<Sender>(c => c.When(ctx => ctx.EndpointsStarted, s => s.Send(new MyMessage())))
                .WithEndpoint<Receiver>()
                .Done(c => c.MessageReceivedByNonMigratedReceiver && c.ReplyReceivedByNonMigratedSender)
                .Run(TimeSpan.FromSeconds(30));

            Assert.IsTrue(beforeMigration.MessageReceivedByNonMigratedReceiver);
            Assert.IsTrue(beforeMigration.ReplyReceivedByNonMigratedSender);
        }

        [Test]
        public async Task Should_not_lose_messages_when_sender_is_migrated()
        {

            var senderMigrated = await Scenario.Define<Context>()
                .WithEndpoint<MigratedSender>(c => c.When(ctx => ctx.EndpointsStarted, s => s.Send(new MyMessage())))
                .WithEndpoint<Receiver>()
                .Done(c => c.MessageReceivedByNonMigratedReceiver && c.ReplyReceivedByMigratedSender >= 1)
                .Run(TimeSpan.FromSeconds(30));

            Assert.IsTrue(senderMigrated.MessageReceivedByNonMigratedReceiver);
            Assert.IsTrue(senderMigrated.ReplyReceivedByMigratedSender >= 1);
        }

        [Test]
        public async Task Should_not_lose_messages_when_receiver_is_migrated()
        {

            var senderMigrated = await Scenario.Define<Context>()
                .WithEndpoint<Sender>(c => c.When(ctx => ctx.EndpointsStarted, s => s.Send(new MyMessage())))
                .WithEndpoint<MigratedReceiver>()
                .Done(c => c.MessagesReceivedByMigratedReceiver >= 1 && c.ReplyReceivedByNonMigratedSender)
                .Run(TimeSpan.FromSeconds(30));

            Assert.IsTrue(senderMigrated.ReplyReceivedByNonMigratedSender);
            Assert.IsTrue(senderMigrated.MessagesReceivedByMigratedReceiver >= 1);
        }

        [Test]
        public async Task Should_not_lose_messages_when_both_sender_and_receiver_is_migrated()
        {
            var receiverMigrated = await Scenario.Define<Context>()
                .WithEndpoint<MigratedSender>(c => c.When(ctx => ctx.EndpointsStarted, s => s.Send(new MyMessage())))
                .WithEndpoint<MigratedReceiver>()
                .Done(c => c.MessagesReceivedByMigratedReceiver >= 1 && c.ReplyReceivedByMigratedSender >= 1)
                .Run(TimeSpan.FromSeconds(30));

            Assert.IsTrue(receiverMigrated.MessagesReceivedByMigratedReceiver >= 1);
            Assert.IsTrue(receiverMigrated.ReplyReceivedByMigratedSender >= 1);
        }

        [Test]
        public async Task Should_not_lose_messages_after_migration()
        {
            var compatModeDisabled = await Scenario.Define<Context>()
                .WithEndpoint<MigratedSenderNoCompatMode>(c => c.When(ctx => ctx.EndpointsStarted, s => s.Send(new MyMessage())))
                .WithEndpoint<MigratedReceiverNoCompatMode>()
                .Done(c => c.MessagesReceivedByMigratedReceiver >= 1 && c.ReplyReceivedByMigratedSender >= 1)
                .Run(TimeSpan.FromSeconds(30));

            Assert.IsTrue(compatModeDisabled.MessagesReceivedByMigratedReceiver >= 1);
            Assert.IsTrue(compatModeDisabled.ReplyReceivedByMigratedSender >= 1);
        }

        class Context : ScenarioContext
        {
            public bool MessageReceivedByNonMigratedReceiver { get; set; }
            public int MessagesReceivedByMigratedReceiver { get; set; }
            public bool ReplyReceivedByNonMigratedSender { get; set; }
            public int ReplyReceivedByMigratedSender { get; set; }
        }

        class Sender : EndpointConfigurationBuilder
        {
            public Sender()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var routing = c.UseTransport<TestTransport>().BrokerAlpha().Routing();
                    routing.RouteToEndpoint(typeof(MyMessage), ReceiverEndpointName);
                });
            }

            class MyReplyHandler : IHandleMessages<MyReply>
            {
                Context scenarioContext;

                public MyReplyHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(MyReply message, IMessageHandlerContext context)
                {
                    scenarioContext.ReplyReceivedByNonMigratedSender = true;
                    return Task.CompletedTask;
                }
            }
        }

        class MigratedSender : EndpointConfigurationBuilder
        {
            public MigratedSender()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var routing = c.EnableTransportMigration<TestTransport, TestTransport>(oldTrans =>
                    {
                        oldTrans.BrokerAlpha();
                    }, newTrans =>
                    {
                        newTrans.BrokerYankee();
                    });
                    routing.RouteToEndpoint(typeof(MyMessage), ReceiverEndpointName);
                }).CustomEndpointName(SenderEndpointName);
            }

            class MyReplyHandler : IHandleMessages<MyReply>
            {
                Context scenarioContext;

                public MyReplyHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(MyReply message, IMessageHandlerContext context)
                {
                    scenarioContext.ReplyReceivedByMigratedSender++;
                    return Task.CompletedTask;
                }
            }
        }

        class MigratedSenderNoCompatMode : EndpointConfigurationBuilder
        {
            public MigratedSenderNoCompatMode()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    var routing = c.UseTransport<TestTransport>().BrokerYankee().Routing();
                    routing.RouteToEndpoint(typeof(MyMessage), ReceiverEndpointName);
                }).CustomEndpointName(SenderEndpointName);
            }

            class MyReplyHandler : IHandleMessages<MyReply>
            {
                Context scenarioContext;

                public MyReplyHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(MyReply message, IMessageHandlerContext context)
                {
                    scenarioContext.ReplyReceivedByMigratedSender++;
                    return Task.CompletedTask;
                }
            }
        }

        class Receiver : EndpointConfigurationBuilder
        {
            public Receiver()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.UseTransport<TestTransport>().BrokerAlpha();
                });
            }

            class MyMessageHandler : IHandleMessages<MyMessage>
            {
                Context scenarioContext;

                public MyMessageHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    scenarioContext.MessageReceivedByNonMigratedReceiver = true;
                    return context.Reply(new MyReply());
                }
            }
        }

        class MigratedReceiver : EndpointConfigurationBuilder
        {
            public MigratedReceiver()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.EnableTransportMigration<TestTransport, TestTransport>(to =>
                    {
                        to.BrokerAlpha();
                    }, tn =>
                    {
                        tn.BrokerYankee();
                    });
                }).CustomEndpointName(ReceiverEndpointName);
            }

            class MyMessageHandler : IHandleMessages<MyMessage>
            {
                Context scenarioContext;

                public MyMessageHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    scenarioContext.MessagesReceivedByMigratedReceiver++;
                    return context.Reply(new MyReply());
                }
            }
        }

        class MigratedReceiverNoCompatMode : EndpointConfigurationBuilder
        {
            public MigratedReceiverNoCompatMode()
            {
                EndpointSetup<DefaultServer>(c =>
                {
                    c.UseTransport<TestTransport>().BrokerYankee();

                }).CustomEndpointName(ReceiverEndpointName);
            }

            class MyMessageHandler : IHandleMessages<MyMessage>
            {
                Context scenarioContext;

                public MyMessageHandler(Context scenarioContext)
                {
                    this.scenarioContext = scenarioContext;
                }

                public Task Handle(MyMessage message, IMessageHandlerContext context)
                {
                    scenarioContext.MessagesReceivedByMigratedReceiver++;
                    return context.Reply(new MyReply());
                }
            }
        }

        class MyMessage : ICommand
        {
        }

        class MyReply : IMessage
        {
        }
    }
}
