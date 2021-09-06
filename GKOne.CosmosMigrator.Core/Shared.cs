using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;

namespace GKOne.CosmosMigrator.Core
{
    public class Shared
    {
        public static CosmosClient CloudClient { get; set; }
        public static CosmosClient LocalClient { get; private set; }

        static Shared()
        {
            var config = new ConfigurationBuilder().AddJsonFile("appsettings.json").Build();
            var localEndpoint = config["CosmosLocalEndpoint"];

            LocalClient = new CosmosClient(localEndpoint);
        }
    }
}
