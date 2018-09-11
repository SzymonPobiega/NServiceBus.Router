using System;
using System.Configuration;

public static class SettingsReader<T>
{
    public static T Read(string name, T defaultValue = default)
    {
        if (TryRead(name, out var value))
        {
            return value;
        }

        return defaultValue;
    }

    public static bool TryRead(string name, out T value)
    {
        if (ConfigurationManager.AppSettings[name] != null)
        {
            value = (T)Convert.ChangeType(ConfigurationManager.AppSettings[name], typeof(T));
            return true;
        }

        value = default;
        return false;
    }
}