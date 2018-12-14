namespace NServiceBus.Router.Deduplication.Inbox
{
    using System;
    using System.Data.SqlClient;

    class LinkState
    {
        public long Epoch { get; }
        public SessionState HeadSession { get; }
        public SessionState TailSession { get; }
        public static LinkState Hydrate(SqlDataReader reader)
        {
            var epoch = reader.GetInt64(0);
            var headLo = reader.GetInt64(1);
            var headHi = reader.GetInt64(2);
            var headTable = reader.IsDBNull(3) ? null : reader.GetString(3);
            var tailLo = reader.GetInt64(4);
            var tailHi = reader.GetInt64(5);
            var tailTable = reader.IsDBNull(6) ? null : reader.GetString(6);

            var headSession = headTable == null ? null : new SessionState(headLo, headHi, headTable);
            var tailSession = tailTable == null ? null : new SessionState(tailLo, tailHi, tailTable);
            return new LinkState(epoch, headSession, tailSession);
        }

        LinkState(long epoch, SessionState headSession, SessionState tailSession)
        {
            Epoch = epoch;
            HeadSession = headSession;
            TailSession = tailSession;
        }

        public LinkState Advance(long nextLo, long nextHi)
        {
            var newHeadSession = new SessionState(nextLo, nextHi, TailSession.Table);
            return new LinkState(Epoch + 1, newHeadSession, HeadSession);
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
                return TailSession.Table;
            }
            throw new Exception($"Provided sequence {sequence} does not match the current epoch [{HeadSession.Lo},{HeadSession.Hi}),[{TailSession.Lo},{TailSession.Hi})");
        }

        public bool IsFromNextEpoch(long seq)
        {
            return seq >= HeadSession.Hi;
        }

        public bool IsDuplicate(long seq)
        {
            return seq < TailSession.Lo;
        }

        public static LinkState Uninitialized()
        {
            return new LinkState(0, null, null);
        }

        public LinkState Initialize(string headTable, long headLo, long headHi, string tailTable, long tailLo, long tailHi)
        {
            if (Initialized)
            {
                throw new NotSupportedException("The link sate object has already been initialized.");
            }
            var newState = new LinkState(1,
                new SessionState(headLo, headHi, headTable),
                new SessionState(tailLo, tailHi, tailTable));

            return newState;
        }

        public override string ToString()
        {
            return $"{Epoch},[{HeadSession.Lo},{HeadSession.Hi}),[{TailSession.Lo},{TailSession.Hi}]";
        }
    }
}