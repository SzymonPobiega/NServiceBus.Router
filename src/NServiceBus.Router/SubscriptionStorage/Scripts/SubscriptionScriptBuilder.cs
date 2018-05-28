using System.IO;
using System.Reflection;
using System.Text;
using NServiceBus.Router;

static class SubscriptionScriptBuilder
{
    static void BuildCreateScript(TextWriter writer, SqlDialect sqlDialect)
    {
        writer.Write(ReadResource(sqlDialect, "Create"));
    }

    public static string BuildCreateScript(SqlDialect sqlDialect)
    {
        var stringBuilder = new StringBuilder();
        using (var stringWriter = new StringWriter(stringBuilder))
        {
            BuildCreateScript(stringWriter, sqlDialect);
        }
        return stringBuilder.ToString();
    }

    static void BuildDropScript(TextWriter writer, SqlDialect sqlDialect)
    {
        writer.Write(ReadResource(sqlDialect, "Drop"));
    }

    public static string BuildDropScript(SqlDialect sqlDialect)
    {
        var stringBuilder = new StringBuilder();
        using (var stringWriter = new StringWriter(stringBuilder))
        {
            BuildDropScript(stringWriter, sqlDialect);
        }
        return stringBuilder.ToString();
    }

    static Assembly assembly = typeof(SubscriptionScriptBuilder).GetTypeInfo().Assembly;

    static string ReadResource(SqlDialect sqlDialect, string prefix)
    {
        var text = $"NServiceBus.Router.SubscriptionStorage.Scripts.{prefix}_{sqlDialect.Name}.sql";
        using (var stream = assembly.GetManifestResourceStream(text))
        using (var streamReader = new StreamReader(stream))
        {
            return streamReader.ReadToEnd();
        }
    }
}

