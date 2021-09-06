using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace GKOne.CosmosMigrator.Core
{
    public class Migrator
    {
        // For progress bar
        public int totalContainers = 0;
        public int totalDocuments = 0;
        public int totalDatabases = 0;
        public event EventHandler DatabaseCompletion;
        public async Task Migrate()
        {
            totalDatabases = await GetDatabases().CountAsync();
            await foreach (var db in GetDatabases())
            {
                await TryCreateDatabase(db);
                await foreach (var container in GetContainers(db))
                {
                    await TryCreateContainer(db, container.Id, container.PartitionKeyPath);
                    var docs = await GetDocuments(db, container.Id);
                    await CreateDocuments(db, container.Id, container.PartitionKeyPath.Replace("/", ""), docs);
                }
                totalDatabases--;
                DatabaseCompletion?.Invoke(this, EventArgs.Empty);
            }
        }

        public void SetCloudClient(string connString)
        {
            try
            {
                Shared.CloudClient = new CosmosClient(connString);
            }
            catch (Exception ex)
            {
                throw new Exception("Connection with Database can't be established.");
            }
        }

        private async Task CreateDocuments(string database, string containerId, string partitionKey, IEnumerable<dynamic> docs)
        {

            var container = Shared.LocalClient.GetContainer(database, containerId);
            var tasks = new List<Task>();

            int transactionBatchSize = 1000;
            List<IEnumerable<dynamic>> batchList = new List<IEnumerable<dynamic>>();

            // Document bulk creation in emulator is limited to 1000
            for (int i = 0; i < docs.Count(); i += transactionBatchSize)
            {
                batchList.Add(docs.ToList().GetRange(i, Math.Min(transactionBatchSize, docs.Count() - i)));
            }

            foreach (var docCollection in batchList)
            {
                foreach (var doc in docCollection)
                {
                    try
                    {
                        var pk = doc[partitionKey]?.ToString() ?? null;
                        Task task;
                        if (pk != null)
                        {
                            task = container.CreateItemAsync(doc, new PartitionKey(pk));
                        }
                        else
                        {
                            task = container.CreateItemAsync(doc);
                        }
                        tasks.Add(task.ContinueWith(t =>
                        {
                            if (t.Status == TaskStatus.Faulted)
                            {
                                //Console.WriteLine($"Error creating document: {t.Exception.Message}");
                            }
                        }));
                    }
                    catch (Exception ex)
                    {
                        throw new Exception($"Unexpected Error creating document: {ex.Message}");
                    }
                }

            }

            await Task.WhenAll(tasks);
        }

        private async Task<IEnumerable<dynamic>> GetDocuments(string database, string containerId)
        {
            var container = Shared.CloudClient.GetContainer(database, containerId);
            var sql = "SELECT * FROM c";
            var iterator = container.GetItemQueryIterator<dynamic>(sql);
            var page = await iterator.ReadNextAsync();

            var docs = new List<dynamic>();

            foreach (var doc in page)
            {
                //Console.WriteLine($"Document: {doc.id}");
                docs.Add(doc);
            }

            return docs;
        }

        private async Task TryCreateContainer(string database, string containerId, string partitionKey)
        {
            var containerDef = new ContainerProperties
            {
                Id = containerId,
                PartitionKeyPath = partitionKey
            };

            var db = Shared.LocalClient.GetDatabase(database);
            try
            {
                await db.CreateContainerAsync(containerDef);
            }
            catch (HttpRequestException ex)
            {
                throw new Exception("Connection with local emulator can't be established");
            }
            catch (CosmosException ex)
            {
                //Console.WriteLine($"Container {containerId} with partition key {partitionKey} already exists");
            }
        }

        private async IAsyncEnumerable<ContainerProperties> GetContainers(string database)
        {
            var db = Shared.CloudClient.GetDatabase(database);
            var iterator = db.GetContainerQueryIterator<ContainerProperties>();
            var containers = await iterator.ReadNextAsync();

            foreach (var container in containers)
            {
                yield return container;
            }
        }

        private async Task TryCreateDatabase(string database)
        {
            try
            {
                await Shared.LocalClient.CreateDatabaseAsync(database);
            }
            catch (HttpRequestException ex)
            {
                throw new Exception("Connection with local emulator can't be established. Turn on the emulator or change the primary connection string to the default value in http://localhost:8081/");
            }
            catch (CosmosException ex)
            {
                if (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    //Console.WriteLine($"Database with id {database} already exists");
                }
            }
        }

        private async static IAsyncEnumerable<string> GetDatabases()
        {
            var iterator = Shared.CloudClient.GetDatabaseQueryIterator<DatabaseProperties>();
            var databases = await iterator.ReadNextAsync();

            var count = 0;

            foreach (var db in databases)
            {
                //Console.WriteLine(db.Id);
                yield return db.Id;
            }
            //Console.WriteLine($"\nTotal Count: {count}");
        }
    }
}
