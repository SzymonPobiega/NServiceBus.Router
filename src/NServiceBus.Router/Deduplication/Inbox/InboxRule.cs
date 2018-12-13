namespace NServiceBus.Router.Deduplication.Inbox
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using Transport;

    class InboxRule : IRule<RawContext, RawContext>
    {
        Func<SqlConnection> connectionFactory;
        DeduplicationSettings settings;
        InboxPersisterCollection persisterCollection;

        public InboxRule(string destinationKey, DeduplicationSettings settings)
        {
            persisterCollection = new InboxPersisterCollection(destinationKey, settings);
            connectionFactory = settings.ConnFactory;
            this.settings = settings;
        }

        static long ParseIntHeader(IReadOnlyDictionary<string, string> headers, string headerKey)
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

        public async Task Invoke(RawContext context, Func<RawContext, Task> next)
        {
            if (!settings.IsInboxEnabledFor(context.Interface))
            {
                await next(context).ConfigureAwait(false);
                return;
            }

            if (!context.Headers.TryGetValue(RouterHeaders.SequenceKey, out var seqKey))
            {
                await next(context).ConfigureAwait(false);
                return;
            }

            if (!settings.IsInboxEnabledFor(context.Interface, seqKey))
            {
                throw new UnforwardableMessageException($"Deduplication is not enabled for source {seqKey} via interface {context.Interface}");
            }

            var seq = ParseIntHeader(context.Headers, RouterHeaders.SequenceNumber);

            var isInitialize = context.Headers.ContainsKey(RouterHeaders.Initialize);
            if (isInitialize)
            {
                using (var conn = connectionFactory())
                {
                    await conn.OpenAsync().ConfigureAwait(false);
                    var headLo = ParseIntHeader(context.Headers, RouterHeaders.InitializeHeadLo);
                    var headHi = ParseIntHeader(context.Headers, RouterHeaders.InitializeHeadHi);
                    var tailLo = ParseIntHeader(context.Headers, RouterHeaders.InitializeTailLo);
                    var tailHi = ParseIntHeader(context.Headers, RouterHeaders.InitializeTailHi);

                    await persisterCollection.Initialize(seqKey, headLo, headHi, tailLo, tailHi, conn).ConfigureAwait(false);
                    return;
                }
            }

            var isAdvance = context.Headers.ContainsKey(RouterHeaders.Advance);
            if (isAdvance)
            {
                using (var conn = connectionFactory())
                {
                    await conn.OpenAsync().ConfigureAwait(false);
                    var nextEpoch = ParseIntHeader(context.Headers, RouterHeaders.AdvanceEpoch);
                    var nextLo = ParseIntHeader(context.Headers, RouterHeaders.AdvanceHeadLo);
                    var nextHi = ParseIntHeader(context.Headers, RouterHeaders.AdvanceHeadHi);

                    await persisterCollection.Advance(seqKey, nextEpoch, nextLo, nextHi, conn).ConfigureAwait(false);
                    return;
                }
            }
            using (var conn = connectionFactory())
            {
                await conn.OpenAsync().ConfigureAwait(false);
                using (var trans = conn.BeginTransaction())
                {
                    var result = await persister.Deduplicate(cleanerCollection.GetLinkState(seqKey), context.MessageId, seq, conn, trans)
                        .ConfigureAwait(false);

                    if (result == DeduplicationResult.Duplicate)
                    {
                        return;
                    }
                    if (result == DeduplicationResult.StaleLinkState)
                    {
                        //The watermarks may be outdated (in which case the retry may solve it) or the
                        //Seq number might be too high for the inbox to fit in which case this exception
                        //will repeat triggering the throttled mode untill the table can be closed
                        throw new ProcessCurrentMessageLaterException("Aborting forwarding due to check constraint violation. " +
                                            "Either watermarks are outdated and this is a valid duplicate (in which case the retry will solve the issue)" +
                                            "or there is a hole in the sequence which prevents closing the epoch.");
                    }

                    cleanerCollection.UpdateReceivedSequence(seqKey, seq);
                    var isPlug = context.Headers.ContainsKey(RouterHeaders.Plug);
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
