using System;
using NServiceBus.Router;

static class TLV
{
    public static string Encode(string type, string value)
    {
        return $"{type}|{value.Length}|{value}";
    }

    public static string AppendTLV(this string existingTLV, string type, string value)
    {
        return existingTLV.TrimEnd('|') + "|" + Encode(type, value);
    }

    public static void DecodeTLV(this string tlvString, Action<string, string> valueCallback)
    {
        var remaining = tlvString;
        while (true)
        {
            var next = remaining.IndexOf("|", StringComparison.Ordinal);
            if (next < 0)
            {
                throw new UnforwardableMessageException("Expected type");
            }
            var type = remaining.Substring(0, next);
            remaining = remaining.Substring(next + 1);

            next = remaining.IndexOf("|", StringComparison.Ordinal);
            if (next < 0)
            {
                throw new UnforwardableMessageException("Expected length");
            }
            var lengthString = remaining.Substring(0, next);
            var length = int.Parse(lengthString);

            remaining = remaining.Substring(next + 1);
            if (remaining.Length < length)
            {
                throw new UnforwardableMessageException($"Expected content of {length} characters");
            }

            var value = remaining.Substring(0, length);

            valueCallback(type, value);

            remaining = remaining.Substring(length);
            if (remaining == "")
            {
                return;
            }
            if (!remaining.StartsWith("|"))
            {
                throw new UnforwardableMessageException("Expected separator");
            }
            remaining = remaining.Substring(1);
        }
    }
}