using System.Threading;

static class InterlocedEx
{
    public static long ExchangeIfGreaterThan(ref long location, long newValue)
    {
        long initialValue;
        do
        {
            initialValue = Interlocked.Read(ref location);
            if (initialValue >= newValue)
            {
                return initialValue;
            }
        }
        while (Interlocked.CompareExchange(ref location, newValue, initialValue) != initialValue);
        return initialValue;
    }
}