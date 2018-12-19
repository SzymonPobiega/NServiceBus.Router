namespace NServiceBus.Router.Deduplication.Inbox
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using Transport;

    class InboxRule : IRule<RawContext, RawContext>
    {
        DeduplicationSettings settings;
        InboxPersisterCollection persisterCollection;

        public InboxRule(InboxPersisterCollection persisterCollection, DeduplicationSettings settings)
        {
            this.persisterCollection = persisterCollection;
            this.settings = settings;
        }

        static long GetInt64Header(IReadOnlyDictionary<string, string> headers, string headerKey)
        {
            if (!headers.TryGetValue(headerKey, out var textValue))
            {
                throw new UnforwardableMessageException($"Missing required value for header {headerKey}.");
            }

            if (!long.TryParse(textValue, out var result))
            {
                throw new UnforwardableMessageException($"Value for header {headerKey} must be an integer.");
            }

            return result;
        }

        public Task Invoke(RawContext context, Func<RawContext, Task> next)
        {
            if (!settings.IsDeduplicationEnabledFor(context.Interface))
            {
                return next(context);
            }

            if (!context.Headers.TryGetValue(RouterDeduplicationHeaders.SequenceKey, out var seqKey))
            {
                return next(context);
            }

            if (!settings.IsDeduplicationEnabledFor(context.Interface, seqKey))
            {
                throw new UnforwardableMessageException($"Deduplication is not enabled for source {seqKey} via interface {context.Interface}");
            }

            var isInitialize = context.Headers.ContainsKey(RouterDeduplicationHeaders.Initialize);
            var isAdvance = context.Headers.ContainsKey(RouterDeduplicationHeaders.Advance);

            if (isInitialize)
            {
                return ProcessInitializeMessage(context, seqKey);
            }
            if (isAdvance)
            {
                return ProcessAdvanceMessage(context, seqKey);
            }

            var seq = GetInt64Header(context.Headers, RouterDeduplicationHeaders.SequenceNumber);
            return ProcessRegularMessage(context, next, seqKey, seq);
        }

        async Task ProcessInitializeMessage(RawContext context, string seqKey)
        {
            using (var conn = settings.Links[seqKey].ConnectionFactory())
            {
                await conn.OpenAsync().ConfigureAwait(false);
                var headLo = GetInt64Header(context.Headers, RouterDeduplicationHeaders.InitializeHeadLo);
                var headHi = GetInt64Header(context.Headers, RouterDeduplicationHeaders.InitializeHeadHi);
                var tailLo = GetInt64Header(context.Headers, RouterDeduplicationHeaders.InitializeTailLo);
                var tailHi = GetInt64Header(context.Headers, RouterDeduplicationHeaders.InitializeTailHi);

                await persisterCollection.Initialize(seqKey, headLo, headHi, tailLo, tailHi, conn).ConfigureAwait(false);
            }
        }

        async Task ProcessAdvanceMessage(RawContext context, string seqKey)
        {
            using (var conn = settings.Links[seqKey].ConnectionFactory())
            {
                await conn.OpenAsync().ConfigureAwait(false);
                var nextEpoch = GetInt64Header(context.Headers, RouterDeduplicationHeaders.AdvanceEpoch);
                var nextLo = GetInt64Header(context.Headers, RouterDeduplicationHeaders.AdvanceHeadLo);
                var nextHi = GetInt64Header(context.Headers, RouterDeduplicationHeaders.AdvanceHeadHi);

                await persisterCollection.Advance(seqKey, nextEpoch, nextLo, nextHi, conn).ConfigureAwait(false);
            }
        }

        async Task ProcessRegularMessage(RawContext context, Func<RawContext, Task> next, string seqKey, long seq)
        {
            using (var conn = settings.Links[seqKey].ConnectionFactory())
            {
                await conn.OpenAsync().ConfigureAwait(false);
                using (var trans = conn.BeginTransaction())
                {
                    var result = await persisterCollection.Deduplicate(seqKey, context.MessageId, seq, conn, trans)
                        .ConfigureAwait(false);

                    if (result == DeduplicationResult.Duplicate)
                    {
                        return;
                    }

                    var isPlug = context.Headers.ContainsKey(RouterDeduplicationHeaders.Plug);
                    if (!isPlug) //If message is only a plug we don't forward it
                    {
                        await Forward(context, next, conn, trans);
                    }

                    trans.Commit();
                }
            }
        }

        static async Task Forward(RawContext context, Func<RawContext, Task> next, SqlConnection conn, SqlTransaction trans)
        {
            var receivedTransportTransaction = context.Extensions.Get<TransportTransaction>();
            var sqlTransportTransaction = new TransportTransaction();
            sqlTransportTransaction.Set(conn);
            sqlTransportTransaction.Set(trans);

            context.Extensions.Set(sqlTransportTransaction);

            await next(context);

            context.Extensions.Set(receivedTransportTransaction);
        }
    }
}
