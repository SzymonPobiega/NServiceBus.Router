namespace NServiceBus.Router
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Data.SqlClient;
    using System.Text;
    using Unicast.Subscriptions;

    public partial class SqlDialect
    {
        /// <summary>
        /// SQL Server dialect
        /// </summary>
        public class MsSqlServer : SqlDialect
        {
            string schema;

            /// <summary>
            /// Microsoft SQL Server
            /// </summary>
            public MsSqlServer()
                : this("dbo")
            {
            }

            /// <summary>
            /// Microsoft SQL Server
            /// </summary>
            public MsSqlServer(string schema)
            {
                this.schema = schema;
            }

            internal override void AddCreationScriptParameters(DbCommand command)
            {
                command.AddParameter("schema", schema);
            }

            internal override void SetParameterValue(DbParameter parameter, object value)
            {
                if (value is ArraySegment<char> charSegment)
                {
                    var sqlParameter = (SqlParameter)parameter;

                    sqlParameter.Value = charSegment.Array;
                    sqlParameter.Offset = charSegment.Offset;
                    sqlParameter.Size = charSegment.Count;
                }
                else
                {
                    parameter.Value = value;
                }
            }

            internal override CommandWrapper CreateCommand(DbConnection connection)
            {
                var command = connection.CreateCommand();
                return new CommandWrapper(command, this);
            }


            internal override string GetSubscriptionTableName(string tablePrefix)
            {
                return $"[{schema}].[{tablePrefix}SubscriptionData]";
            }

            internal override string GetSubscriptionSubscribeCommand(string tableName)
            {
                return $@"
declare @dummy int;
merge {tableName} with (holdlock, tablock) as target
using(select @Endpoint as Endpoint, @Subscriber as Subscriber, @MessageType as MessageType) as source
on target.Subscriber = source.Subscriber 
    and target.MessageType = source.MessageType 
    and ((target.Endpoint = source.Endpoint) or (target.Endpoint is null and source.endpoint is null))
when not matched then
insert
(
    Subscriber,
    MessageType,
    Endpoint,
    PersistenceVersion
)
values
(
    @Subscriber,
    @MessageType,
    @Endpoint,
    @PersistenceVersion
);";
            }

            internal override string GetSubscriptionUnsubscribeCommand(string tableName)
            {
                return $@"
delete from {tableName}
where
    Subscriber = @Subscriber and
    MessageType = @MessageType";
            }

            internal override Func<List<MessageType>, string> GetSubscriptionQueryFactory(string tableName)
            {
                var getSubscribersPrefix = $@"
select distinct Subscriber, Endpoint
from {tableName}
where MessageType in (";

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