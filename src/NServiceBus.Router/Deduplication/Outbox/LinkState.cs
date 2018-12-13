namespace NServiceBus.Router.Deduplication
{
    using System;
    using System.Collections.Generic;
    using System.Data.SqlClient;
    using Transport;

    class LinkState
    {
        public long Epoch { get; }
        /// <summary>
        /// Has the recent epoch change been announced.
        /// </summary>
        public bool IsEpochAnnounced { get; }
        public SessionState HeadSession { get; }
        public SessionState TailSession { get; }

        public static LinkState Hydrate(SqlDataReader reader)
        {
            var epoch = reader.GetInt64(0);
            var announced = reader.GetBoolean(1);
            var headLo = reader.GetInt64(2);
            var headHi = reader.GetInt64(3);
            var headTable = reader.GetString(4);
            var tailLo = reader.GetInt64(5);
            var tailHi = reader.GetInt64(6);
            var tailTable = reader.GetString(7);

            return new LinkState(epoch, announced, new SessionState(headLo, headHi, headTable), new SessionState(tailLo, tailHi, tailTable));
        }

        LinkState(long epoch, bool announced, SessionState headSession, SessionState tailSession)
        {
            Epoch = epoch;
            IsEpochAnnounced = announced;
            HeadSession = headSession;
            TailSession = tailSession;
        }

        public LinkState Advance(int epochSize)
        {
            return new LinkState(Epoch + 1, false,
                new SessionState(HeadSession.Hi, HeadSession.Hi + epochSize, TailSession.Table), 
                HeadSession);
        }

        public (LinkState, OutgoingMessage) AnnounceAdvance(string sourceKey)
        {
            var newState = new LinkState(Epoch, true, HeadSession, TailSession);

            var headers = new Dictionary<string, string>
            {
                [RouterHeaders.SequenceKey] = sourceKey,
                [RouterHeaders.Advance] = "true",
                [RouterHeaders.AdvanceEpoch] = Epoch.ToString(),
                [RouterHeaders.AdvanceHeadLo] = HeadSession.Lo.ToString(),
                [RouterHeaders.AdvanceHeadHi] = HeadSession.Hi.ToString(),
            };

            var message = new OutgoingMessage(Guid.NewGuid().ToString(), headers, new byte[0]);
            return (newState, message);
        }

        public (LinkState, OutgoingMessage) AnnounceInitialize(string sourceKey)
        {
            var newState = new LinkState(Epoch, true, HeadSession, TailSession);

            var headers = new Dictionary<string, string>
            {
                [RouterHeaders.SequenceKey] = sourceKey,
                [RouterHeaders.Initialize] = "true",
                [RouterHeaders.InitializeHeadLo] = HeadSession.Lo.ToString(),
                [RouterHeaders.InitializeHeadHi] = HeadSession.Hi.ToString(),
                [RouterHeaders.InitializeTailLo] = TailSession.Lo.ToString(),
                [RouterHeaders.InitializeTailHi] = TailSession.Hi.ToString()
            };

            var message = new OutgoingMessage(Guid.NewGuid().ToString(), headers, new byte[0]);
            return (newState, message);
        }

        public bool Initialized => HeadSession != null;

        public string GetTableName(long sequence)
        {
            if (HeadSession.Matches(sequence))
            {
                return HeadSession.Table;
            }
            if (TailSession.Matches(sequence))
            {
                return HeadSession.Table;
            }
            throw new Exception($"Provided sequence {sequence} does not match the current epoch [{HeadSession.Lo},{HeadSession.Hi}),[{TailSession.Lo},{TailSession.Hi})");
        }

        public static LinkState Uninitialized()
        {
            return new LinkState(0, false, null, null);
        }

        public LinkState Initialize(string headTable, string tailTable, int epochSize)
        {
            if (Initialized)
            {
                throw new NotSupportedException("The link sate object has already been initialized.");
            }
            var newState = new LinkState(1, false,
                new SessionState(epochSize, epochSize + epochSize, headTable),
                new SessionState(0, epochSize, tailTable));

            return newState;
        }

        public override string ToString()
        {
            return $"{Epoch},[{HeadSession.Lo},{HeadSession.Hi}),[{TailSession.Lo},{TailSession.Hi}]";
        }
    }
}