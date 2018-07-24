namespace NServiceBus.Router.Deduplication
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using System.Threading.Tasks;
    using Transport;

    class OutboxPersistence
    {
        const int EpochSize = 10000;

        public Task<List<CapturedOutgoingMessages>> GetUndispatchedOutgoingMessageses(string incomingMessageId)
        {
            throw new System.NotImplementedException();
        }

        public async Task Store(List<CapturedOutgoingMessages> capturedMessages, TransportTransaction transportTransaction, Func<long, Task> dispatchPlug)
        {
            var conn = transportTransaction.Get<SqlConnection>(); //TODO: Open new conection is in transaction scope mode
            var trans = transportTransaction.Get<SqlTransaction>();

            foreach (var messageGroup in capturedMessages)
            {
                foreach (var operation in messageGroup.Operations.UnicastTransportOperations)
                {
                    await Store(operation.Message, operation.Destination, dispatchPlug, conn, trans).ConfigureAwait(false);
                }
                foreach (var operation in messageGroup.Operations.MulticastTransportOperations)
                {
                    await Store(operation.Message, operation.MessageType.FullName, dispatchPlug, conn, trans).ConfigureAwait(false);
                }
            }
        }

        async Task Store(OutgoingMessage message, string sequenceKey, Func<long, Task> dispatchPlug, SqlConnection conn, SqlTransaction trans)
        {
            var seq = await GetNextSequenceValue(sequenceKey, conn).ConfigureAwait(false);

            var side = (seq / EpochSize) % 2;

            var tableName = $"Outbox_{sequenceKey}_{side}";

            await EnsureBelowHighWaterMark(sequenceKey, tableName, seq, dispatchPlug, conn).ConfigureAwait(false);

            await InsertAndCheckLowWaterMark(message, sequenceKey, tableName, seq, conn, trans);
        }

        async Task InsertAndCheckLowWaterMark(OutgoingMessage message, string sequenceKey, string tableName, long seq, SqlConnection conn, SqlTransaction trans)
        {
            using (var command = new SqlCommand($@"
insert into {tableName} (Seq, MessageId, Headers, Body, Dispatched, IsPlug) values (@seq, @messageId, @headers, @body, 0, 0);
select Lo from WaterMarks where Destination = @key", 
                conn, trans))
            {
                command.Parameters.AddWithValue("@seq", seq);
                command.Parameters.AddWithValue("@key", sequenceKey);
                command.Parameters.AddWithValue("@messageId", message.MessageId);
                command.Parameters.AddWithValue("@headers", message.Headers);
                command.Parameters.AddWithValue("@body", message.Body);
                var reader = await command.ExecuteReaderAsync().ConfigureAwait(false);
                if (!reader.NextResult())
                {
                    throw new Exception($"WaterMarks table is missing marks for destination '{sequenceKey}'");
                }

                var lowWaterMark = reader.GetInt64(0);
                if (seq < lowWaterMark)
                {
                    throw new Exception("Generated sequence number is below current low water mark.");
                }
            }
        }

        async Task EnsureBelowHighWaterMark(string sequenceKey, string tableName, long seq, Func<long, Task> dispatchPlug, SqlConnection conn)
        {
            if (seq < highWaterMark)
            {
                return;
            }

            highWaterMark = await GetHighWaterMark(sequenceKey, conn).ConfigureAwait(false);

            if (seq < highWaterMark)
            {
                return;
            }

            while (await HasHoles(tableName, conn).ConfigureAwait(false))
            {
                var holes = await FindHoles(tableName, highWaterMark, conn).ConfigureAwait(false);

                //Ensure only one process can enter here
                using (var lockTransaction = conn.BeginTransaction())
                {
                    highWaterMark = await GetHighWaterMarkWithLock(sequenceKey, conn, lockTransaction);
                    if (seq <= highWaterMark)
                    {
                        //Somebody else closed the epoch
                        return;
                    }

                    var plugHolesConnection = new SqlConnection(conn.ConnectionString);
                    await plugHolesConnection.OpenAsync().ConfigureAwait(false);

                    foreach (var holeId in holes)
                    {
                        await PlugHole(tableName, holeId, conn).ConfigureAwait(false);
                    }

                    var undispatchedPlugs = await FindUndispatchedPlugs(tableName, plugHolesConnection);
                    foreach (var plugId in undispatchedPlugs)
                    {
                        await dispatchPlug(plugId).ConfigureAwait(false);
                        await MarkAsDispatched(tableName, plugId, plugHolesConnection, null).ConfigureAwait(false);
                    }

                    await UpdateLowWaterMark(sequenceKey, conn, lockTransaction).ConfigureAwait(false);

                    await Truncate(tableName, conn, lockTransaction).ConfigureAwait(false);

                    await UpdateHiWaterMark(sequenceKey, conn, lockTransaction).ConfigureAwait(false);

                    lockTransaction.Commit();
                }
            }
        }

        static async Task PlugHole(string tableName, long holeId, SqlConnection conn)
        {
            using (var command = new SqlCommand($"insert into {tableName} (Seq, MessageId, IsPlug) values (@seq, @id, 1)", conn))
            {
                command.Parameters.AddWithValue("@seq", holeId);
                command.Parameters.AddWithValue("@id", Guid.NewGuid().ToString());
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        static async Task MarkAsDispatched(string tableName, long seq, SqlConnection conn, SqlTransaction trans)
        {
            using (var command = new SqlCommand($"update {tableName} set Dispatched = 1 where Seq = @seq", conn, trans))
            {
                command.Parameters.AddWithValue("@seq", seq);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        static async Task<bool> HasHoles(string tableName, SqlConnection conn)
        {
            using (var command = new SqlCommand($"select count(Seq) from {tableName} where Dispatched = 1", conn))
            {
                var count = (int)await command.ExecuteScalarAsync().ConfigureAwait(false);

                return count != EpochSize;
            }
        }

        static async Task Truncate(string tableName, SqlConnection conn, SqlTransaction trans)
        {
            using (var command = new SqlCommand($"truncate table {tableName}", conn, trans))
            {
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        static async Task<List<long>> FindUndispatchedPlugs(string tableName, SqlConnection conn)
        {
            var plugs = new List<long>();
            using (var command = new SqlCommand($"select Seq from {tableName} where IsPlug = 1 and Dispatched = 0", conn))
            {
                using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    while (reader.NextResult())
                    {
                        plugs.Add(reader.GetInt64(0));
                    }
                }
            }
            return plugs;
        }

        static async Task<List<long>> FindHoles(string tableName, long highWaterMark, SqlConnection conn)
        {
            var holes = new List<long>();
            using (var command = new SqlCommand($@"
select PrevSeq, Seq, NextSeq
from (select Seq, LAG(Seq) over (order by Seq) PrevSeq, LEAD(Seq) over (order by Seq) NextSeq from {tableName}) q 
	where (PrevSeq is null and Seq > @lo) or (NextSeq is null and Seq < @top) or (PrevSeq <> Seq - 1) or (NextSeq <> Seq + 1)
order by Seq
", conn))
            {
                var hi = highWaterMark - EpochSize - 1;
                var lo = highWaterMark - EpochSize - EpochSize;
                command.Parameters.AddWithValue("@hi", hi);
                command.Parameters.AddWithValue("@lo", lo);
                using (var reader = await command.ExecuteReaderAsync().ConfigureAwait(false))
                {
                    while (reader.NextResult())
                    {
                        long l, h;
                        if (reader.IsDBNull(0))
                        {
                            l = 0;
                            h = reader.GetInt64(1) - 1;
                        }
                        else if (reader.IsDBNull(2))
                        {
                            l = reader.GetInt64(1) + 1;
                            h = highWaterMark - 1;
                        }
                        else
                        {
                            l = reader.GetInt64(1) + 1;
                            h = reader.GetInt64(2) - 1;
                        }
                        for (var i = l; i <= h; i++)
                        {
                            holes.Add(i);
                        }
                    }
                }
            }
            return holes;
        }

        static async Task<long> GetHighWaterMark(string sequenceKey, SqlConnection conn)
        {
            using (var command = new SqlCommand("select Hi from WaterMarks where Destination = @key", conn))
            {
                command.Parameters.AddWithValue("@key", sequenceKey);
                var result = await command.ExecuteScalarAsync().ConfigureAwait(false);
                return (long)result;
            }
        }

        static async Task<long> GetHighWaterMarkWithLock(string sequenceKey, SqlConnection conn, SqlTransaction trans)
        {
            using (var command = new SqlCommand("select Hi from WaterMarks with (updlock) where Destination = @key", conn, trans))
            {
                command.Parameters.AddWithValue("@key", sequenceKey);
                var result = await command.ExecuteScalarAsync().ConfigureAwait(false);
                return (long)result;
            }
        }

        static async Task UpdateLowWaterMark(string sequenceKey, SqlConnection conn, SqlTransaction trans)
        {
            using (var command = new SqlCommand("update WaterMarks Lo = Lo + @increment where Destination = @key", conn, trans))
            {
                command.Parameters.AddWithValue("@key", sequenceKey);
                command.Parameters.AddWithValue("@increment", EpochSize);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        static async Task UpdateHiWaterMark(string sequenceKey, SqlConnection conn, SqlTransaction trans)
        {
            using (var command = new SqlCommand("update WaterMarks Hi = Hi + @increment where Destination = @key", conn, trans))
            {
                command.Parameters.AddWithValue("@key", sequenceKey);
                command.Parameters.AddWithValue("@increment", EpochSize);
                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
            }
        }

        Task<long> GetNextSequenceValue(string sequenceKey, SqlConnection conn)
        {
            throw new System.NotImplementedException();
        }

        public Task MarkDispatched(List<CapturedOutgoingMessages> capturedMessages)
        {
            foreach (var messages in capturedMessages)
            {
                foreach (var operation in messages.Operations)
                {
                    using (var command = new SqlCommand($"update {tableName} set Dispatched = 1 where Seq = @seq", conn, trans))
                    {
                        command.Parameters.AddWithValue("@seq", seq);
                        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                    }
                }
            }
        }

        volatile long highWaterMark;
    }
}