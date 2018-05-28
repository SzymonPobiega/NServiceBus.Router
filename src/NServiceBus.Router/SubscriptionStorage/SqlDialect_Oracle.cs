namespace NServiceBus.Router
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.Common;
    using System.Reflection;
    using System.Text;
    using Unicast.Subscriptions;

    public partial class SqlDialect
    {
        /// <summary>
        /// Oracle dialect
        /// </summary>
        public class Oracle : SqlDialect
        {
            volatile PropertyInfo bindByName;
            
            internal override void SetParameterValue(DbParameter parameter, object value)
            {
                if (value is Guid)
                {
                    parameter.Value = value.ToString();
                    return;
                }

                if (value is Version)
                {
                    parameter.DbType = DbType.String;
                    parameter.Value = value.ToString();
                    return;
                }

                parameter.Value = value;
            }

            internal override CommandWrapper CreateCommand(DbConnection connection)
            {
                var command = connection.CreateCommand();

                if (bindByName == null)
                {
                    var type = command.GetType();
                    bindByName = type.GetProperty("BindByName");
                    if (bindByName == null)
                    {
                        throw new Exception($"Could not extract field 'BindByName' from '{type.AssemblyQualifiedName}'.");
                    }
                }
                bindByName.SetValue(command, true);

                return new CommandWrapper(command, this);
            }

            internal string Schema { get; set; }

            string SchemaPrefix => Schema != null ? $"\"{Schema.ToUpper()}\"." : "";
            internal override string GetSubscriptionTableName(string tablePrefix)
            {
               return $"{SchemaPrefix}\"{tablePrefix.ToUpper()}SS\"";
            }

            internal override string GetSubscriptionSubscribeCommand(string tableName)
            {
                return $@"
begin
    insert into {tableName}
    (
        MessageType,
        Subscriber,
        Endpoint,
        PersistenceVersion
    )
    values
    (
        :MessageType,
        :Subscriber,
        :Endpoint,
        :PersistenceVersion
    );
    commit;
exception
    when DUP_VAL_ON_INDEX
    then ROLLBACK;
end;
";
            }

            internal override string GetSubscriptionUnsubscribeCommand(string tableName)
            {
                return $@"
delete from {tableName}
where
    Subscriber = :Subscriber and
    MessageType = :MessageType";
            }

            internal override Func<List<MessageType>, string> GetSubscriptionQueryFactory(string tableName)
            {
                var getSubscribersPrefixOracle = $@"
select distinct Subscriber, Endpoint
from {tableName}
where MessageType in (";

                return messageTypes =>
                {
                    var builder = new StringBuilder(getSubscribersPrefixOracle);
                    for (var i = 0; i < messageTypes.Count; i++)
                    {
                        var paramName = $":type{i}";
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