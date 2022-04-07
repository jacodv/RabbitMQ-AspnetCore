using System;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Events;
using UtilityData.Data.Interfaces;

namespace UtilityData.Data
{
  public static class MongoHelpers
  {
    public static IMongoClient GetMongoClient(this IDatabaseSettings settings)
    {
      var mongoClientSettings = MongoClientSettings.FromConnectionString(settings.ConnectionString);
      mongoClientSettings.ApplicationName = settings.ApplicationName;
      if (!settings.DisableTrace)
      {
        mongoClientSettings.ClusterConfigurator = cb =>
        {
          // This will print the executed command to the console
          cb.Subscribe<CommandStartedEvent>(e =>
          {
            Console.WriteLine($"{e.CommandName} - {e.Command.ToJson()}");
          });
          cb.Subscribe<CommandSucceededEvent>(e =>
          {
            Console.WriteLine($"{e.CommandName} - {e.Duration}");
          });
        };
      }
      return new MongoClient(mongoClientSettings);
    }
  }
}
