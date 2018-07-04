using System;

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
                throw new Exception($"Expected type followed by a delimiter, found '{remaining}'");
            }
            var type = remaining.Substring(0, next);
            remaining = remaining.Substring(next + 1);

            next = remaining.IndexOf("|", StringComparison.Ordinal);
            if (next < 0)
            {
                throw new Exception($"Expected length followed by a delimiter, found '{remaining}'");
            }
            var lengthString = remaining.Substring(0, next);
            int length;
            try
            {
                length = int.Parse(lengthString);
            }
            catch (Exception)
            {
                throw new Exception($"Expected length to be a valid integer, found '{lengthString}'");
            }

            remaining = remaining.Substring(next + 1);
            if (remaining.Length < length)
            {
                throw new Exception($"Expected content of {length} characters, found '{remaining}'");
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
                throw new Exception($"Expected delimiter, found {remaining}");
            }
            remaining = remaining.Substring(1);
        }
    }
}