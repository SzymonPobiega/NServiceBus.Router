namespace NServiceBus.Router.Deduplication
{
    using System.Collections.Generic;
    using System.IO;
    using System.Runtime.Serialization.Json;
    using System.Text;

    static class DictionarySerializer
    {
        static DataContractJsonSerializer serializer = BuildSerializer();

        public static string Serialize(Dictionary<string, string> instance)
        {
            using (var stream = new MemoryStream())
            {
                serializer.WriteObject(stream, instance);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        public static Dictionary<string, string> Deserialize(string json)
        {
            if (json == null)
            {
                return new Dictionary<string, string>();
            }
            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(json)))
            {
                return (Dictionary<string, string>)serializer.ReadObject(stream);
            }
        }

        static DataContractJsonSerializer BuildSerializer()
        {
            var settings = new DataContractJsonSerializerSettings
            {
                UseSimpleDictionaryFormat = true
            };
            return new DataContractJsonSerializer(typeof(Dictionary<string, string>), settings);
        }
    }
}