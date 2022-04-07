using MongoDB.Driver;
using RabbitMQ.Models;
using UtilityData.Data;
using UtilityData.Data.Interfaces;

namespace RabbitMQ.AppServer1.StartUp;

public static class SetupDatabase
{
  public const string TestSiteName = "TestSite";

  public static async Task Init(IServiceProvider serviceProvider)
  {
    var batchItemRepository = ((MongoRepository<BatchItem>)serviceProvider.GetService<IRepository<BatchItem>>()!);

    await _createBatchItemIndexes(batchItemRepository._collection);
  }

  private static async Task _createBatchItemIndexes(IMongoCollection<BatchItem> collection)
  {
    var indexKeys = Builders<BatchItem>.IndexKeys.Ascending(x => x.Batch.Id);
    await _createIndex(collection, indexKeys, false);
  }

  private static async Task _createIndex<T>(IMongoCollection<T> userCollection, IndexKeysDefinition<T> indexKeys, bool isUnique = true)
  {
    var createIndexOptions = new CreateIndexOptions()
    {
      Unique = isUnique
    };
    var createIndexModel = new CreateIndexModel<T>(indexKeys, createIndexOptions);
    await userCollection.Indexes.CreateOneAsync(createIndexModel);
  }

}