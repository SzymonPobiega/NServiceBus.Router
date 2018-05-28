using System;
using System.IO;
using NServiceBus;
using NUnit.Framework;

public static class ConfigureEndpointTestTransport
{
    public static TransportExtensions<TestTransport> BrokerAlpha(this TransportExtensions<TestTransport> transportConfig)
    {
        Configure(transportConfig, "Alpha");
        transportConfig.NoNativePubSub();
        return transportConfig;
    }

    public static TransportExtensions<TestTransport> BrokerBravo(this TransportExtensions<TestTransport> transportConfig)
    {
        Configure(transportConfig, "Bravo");
        transportConfig.NoNativePubSub();
        return transportConfig;
    }

    public static TransportExtensions<TestTransport> BrokerCharlie(this TransportExtensions<TestTransport> transportConfig)
    {
        Configure(transportConfig, "Charlie");
        transportConfig.NoNativePubSub();
        return transportConfig;
    }

    public static TransportExtensions<TestTransport> BrokerDelta(this TransportExtensions<TestTransport> transportConfig)
    {
        Configure(transportConfig, "Delta");
        transportConfig.NoNativePubSub();
        return transportConfig;
    }

    public static TransportExtensions<TestTransport> BrokerYankee(this TransportExtensions<TestTransport> transportConfig)
    {
        Configure(transportConfig, "Yankee");
        return transportConfig;
    }

    public static TransportExtensions<TestTransport> BrokerZulu(this TransportExtensions<TestTransport> transportConfig)
    {
        Configure(transportConfig, "Zulu");
        return transportConfig;
    }

    static void Configure(TransportExtensions<TestTransport> transportConfig, string brokerId)
    {
        var testRunId = TestContext.CurrentContext.Test.ID;

        string tempDir;

        if (Environment.OSVersion.Platform == PlatformID.Win32NT)
        {
            //can't use bin dir since that will be too long on the build agents
            tempDir = @"c:\temp";
        }
        else
        {
            tempDir = Path.GetTempPath();
        }

        var storageDir = Path.Combine(tempDir, testRunId, brokerId);

        transportConfig.StorageDirectory(storageDir);
    }
}