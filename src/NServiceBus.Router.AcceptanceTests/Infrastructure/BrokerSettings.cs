namespace NServiceBus.Router.AcceptanceTests
{
    using System.IO;
    using NUnit.Framework;

    public class BrokerSettings
    {
        AcceptanceTestingTransport transportDefinition;

        public BrokerSettings(AcceptanceTestingTransport transportDefinition)
        {
            this.transportDefinition = transportDefinition;
        }

        void SetStoragePath(string broker)
        {
            var storageDir = Path.Combine(Path.GetTempPath(), "learn", TestContext.CurrentContext.Test.ID, broker);
            transportDefinition.StorageLocation = storageDir;
        }
        public void Alpha()
        {
            SetStoragePath("Alpha");
        }

        public void Bravo()
        {
            SetStoragePath("Bravo");
        }

        public void Charlie()
        {
            SetStoragePath("Charlie");
        }
        
        public void Yankee()
        {
            SetStoragePath("Yankee");
        }

        public void Zulu()
        {
            SetStoragePath("Zulu");
        }
    }
}