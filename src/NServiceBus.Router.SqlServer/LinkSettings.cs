namespace NServiceBus.Router
{
    using System;
    using System.Data.SqlClient;

    class LinkSettings
    {
        public LinkSettings(string sqlInterface, string linkInterface, Func<SqlConnection> connectionFactory, int epochSize)
        {
            LinkInterface = linkInterface;
            ConnectionFactory = connectionFactory;
            EpochSize = epochSize;
            SqlInterface = sqlInterface;
        }

        public string SqlInterface { get; }
        public string LinkInterface { get; }
        public Func<SqlConnection> ConnectionFactory { get; }
        public int EpochSize { get; }
    }
}