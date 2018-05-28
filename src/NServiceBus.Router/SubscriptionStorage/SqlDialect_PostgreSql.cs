namespace NServiceBus.Router
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.Common;
    using System.Text;
    using Unicast.Subscriptions;

    public partial class SqlDialect
    {
        /// <summary>
        /// PostgreSQL dialect
        /// </summary>
        public class PostgreSql : SqlDialect
        {
            /// <summary>
            /// PostgreSQL
            /// </summary>
            public PostgreSql()
            {
                Schema = "public";
            }

            internal override void AddCreationScriptParameters(DbCommand command)
            {
                command.AddParameter("schema", Schema);
            }

            internal override void SetParameterValue(DbParameter parameter, object value)
            {
                if (value is DateTime)
                {
                    parameter.DbType = DbType.DateTime;
                }
                parameter.Value = value;
            }

            internal override CommandWrapper CreateCommand(DbConnection connection)
            {
                var command = connection.CreateCommand();
                return new CommandWrapper(command, this);
            }

            internal string Schema { get; set; }

            internal override string GetSubscriptionTableName(string tablePrefix)
            {
                return $"\"{Schema}\".\"{tablePrefix}SubscriptionData\"";
            }

            internal override string GetSubscriptionSubscribeCommand(string tableName)
            {
                return $@"
insert into {tableName}
(
    ""Id"",
    ""Subscriber"",
    ""MessageType"",
    ""Endpoint"",
    ""PersistenceVersion""
)
values
(
    concat(@Subscriber, @MessageType),
    @Subscriber,
    @MessageType,
    @Endpoint,
    @PersistenceVersion
)
on conflict (""Id"") do update
    set ""Endpoint"" = @Endpoint,
        ""PersistenceVersion"" = @PersistenceVersion
";
            }

            internal override string GetSubscriptionUnsubscribeCommand(string tableName)
            {
                return $@"
delete from {tableName}
where
    ""Subscriber"" = @Subscriber and
    ""MessageType"" = @MessageType";
            }

            internal override Func<List<MessageType>, string> GetSubscriptionQueryFactory(string tableName)
            {
                var getSubscribersPrefix = $@"
select distinct ""Subscriber"", ""Endpoint""
from {tableName}
where ""MessageType"" in (";

                return messageTypes =>
                {
                    var builder = new StringBuilder(getSubscribersPrefix);
                    for (var i = 0; i < messageTypes.Count; i++)
                    {
                        var paramName = $"@type{i}";
                        builder.Append(paramName);
                        if (i < messageTypes.Count - 1)
                        {
                            builder.Append(", ");
                        }
                    }
                    builder.Append(")");
                    return builder.ToString();
                };
            }
        }
    }
}