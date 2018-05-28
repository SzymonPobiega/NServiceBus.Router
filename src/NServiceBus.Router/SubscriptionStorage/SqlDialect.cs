namespace NServiceBus.Router
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Threading.Tasks;
    using Unicast.Subscriptions;

    /// <summary>
    /// Represents a dialect of SQL language.
    /// </summary>
    public abstract partial class SqlDialect
    {
        internal string Name => GetType().Name;

        /// <summary>
        /// Gets the name of the SqlDialect.
        /// </summary>
        public override string ToString()
        {
            return Name;
        }

        internal virtual void AddCreationScriptParameters(DbCommand command)
        {
        }

        internal void AddParameter(DbParameter parameter, string paramName, object value)
        {
            parameter.ParameterName = paramName;
            SetParameterValue(parameter, value);
        }

        internal abstract void SetParameterValue(DbParameter parameter, object value);

        internal abstract CommandWrapper CreateCommand(DbConnection connection);
        internal async Task ExecuteTableCommand(DbConnection connection, DbTransaction transaction, string script, string tablePrefix)
        {
            using (var command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = script;
                command.AddParameter("tablePrefix", tablePrefix);
                AddCreationScriptParameters(command);
                await command.ExecuteNonQueryEx().ConfigureAwait(false);
            }
        }

        internal virtual void ValidateTablePrefix(string tablePrefix)
        {
        }

        internal abstract string GetSubscriptionTableName(string tablePrefix);

        internal abstract string GetSubscriptionSubscribeCommand(string tableName);
        internal abstract string GetSubscriptionUnsubscribeCommand(string tableName);
        internal abstract Func<List<MessageType>, string> GetSubscriptionQueryFactory(string tableName);
    }
}