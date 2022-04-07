using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading.Tasks;
using FluentValidation;
using MongoDB.Bson;
using MongoDB.Driver;
using MongoDB.Driver.Core.Events;
using UtilityData.Data.Interfaces;

namespace UtilityData.Data
{
  public class MongoRepository<TDocument> : IRepository<TDocument>
    where TDocument : IDocument
  {
    private readonly IValidator<TDocument> _validator;
    public readonly IMongoCollection<TDocument> _collection;

    public MongoRepository(IDatabaseSettings settings, IValidator<TDocument> validator)
    {
      _validator = validator;

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
      var client = new MongoClient(mongoClientSettings);
      var database = client.GetDatabase(settings.DatabaseName);
      _collection = database.GetCollection<TDocument>(GetCollectionName(typeof(TDocument)));
    }

    public IMongoCollection<TDocument> Collection => _collection;

    public virtual IQueryable<TDocument> AsQueryable()
    {
      return _collection.AsQueryable();
    }

    public virtual IEnumerable<TDocument> FilterBy(Expression<Func<TDocument, bool>> filterExpression)
    {
      return _collection.Find(filterExpression).ToEnumerable();
    }

    public virtual IEnumerable<TProjected> FilterBy<TProjected>(
        Expression<Func<TDocument, bool>> filterExpression,
        Expression<Func<TDocument, TProjected>> projectionExpression)
    {
      return _collection.Find(filterExpression).Project(projectionExpression).ToEnumerable();
    }

    public virtual TDocument FindOne(Expression<Func<TDocument, bool>> filterExpression)
    {
      return _collection.Find(filterExpression).FirstOrDefault();
    }

    public virtual Task<TDocument> FindOneAsync(Expression<Func<TDocument, bool>> filterExpression)
    {
      return Task.Run(() => _collection.Find(filterExpression).FirstOrDefaultAsync());
    }

    public virtual TDocument FindById(string id)
    {
      var filter = Builders<TDocument>.Filter.Eq(doc => doc.Id, id);
      return _collection.Find(filter).SingleOrDefault();
    }

    public virtual Task<TDocument> FindByIdAsync(string id)
    {
      return Task.Run(() =>
      {
        var filter = Builders<TDocument>.Filter.Eq(doc => doc.Id, id);
        return _collection.Find(filter).SingleOrDefaultAsync();
      });
    }

    public virtual void InsertOne(TDocument document)
    {
      _validate(document);
      _collection.InsertOne(document);
    }

    public virtual Task InsertOneAsync(TDocument document)
    {
      _validate(document);
      return Task.Run(() => _collection.InsertOneAsync(document));
    }

    public void InsertMany(ICollection<TDocument> documents)
    {
      foreach (var document in documents)
        _validate(document);
      _collection.InsertMany(documents);
    }

    public virtual async Task InsertManyAsync(ICollection<TDocument> documents)
    {
      foreach (var document in documents)
        _validate(document);
      await _collection.InsertManyAsync(documents);
    }

    public void ReplaceOne(TDocument document)
    {
        _validate(document);
      var filter = Builders<TDocument>.Filter.Eq(doc => doc.Id, document.Id);
      _collection.FindOneAndReplace(filter, document);
    }

    public virtual async Task ReplaceOneAsync(TDocument document)
    {
      _validate(document);
      var filter = Builders<TDocument>.Filter.Eq(doc => doc.Id, document.Id);
      await _collection.FindOneAndReplaceAsync(filter, document, new FindOneAndReplaceOptions<TDocument, TDocument>(){IsUpsert = true});
    }

    public void DeleteOne(Expression<Func<TDocument, bool>> filterExpression)
    {
      _collection.FindOneAndDelete(filterExpression);
    }

    public Task DeleteOneAsync(Expression<Func<TDocument, bool>> filterExpression)
    {
      return Task.Run(() => _collection.FindOneAndDeleteAsync(filterExpression));
    }

    public void DeleteById(string id)
    {
      var filter = Builders<TDocument>.Filter.Eq(doc => doc.Id, id);
      _collection.FindOneAndDelete(filter);
    }

    public Task DeleteByIdAsync(string id)
    {
      return Task.Run(() =>
      {
        var filter = Builders<TDocument>.Filter.Eq(doc => doc.Id, id);
        _collection.FindOneAndDeleteAsync(filter);
      });
    }

    public void DeleteMany(Expression<Func<TDocument, bool>> filterExpression)
    {
      _collection.DeleteMany(filterExpression);
    }

    public Task DeleteManyAsync(Expression<Func<TDocument, bool>> filterExpression)
    {
      return Task.Run(() => _collection.DeleteManyAsync(filterExpression));
    }

    public Task UpdateById(string id, UpdateDefinition<TDocument> update)
    {
      var filter = Builders<TDocument>.Filter.Eq(doc => doc.Id, id);
      return _collection.FindOneAndUpdateAsync(filter, update);
    }

    #region Private
    private void _validate(TDocument modelToValidate)
    {
      _validator?.ValidateAndThrow(modelToValidate);
    }
    private protected string GetCollectionName(Type documentType)
    {
      return ((BsonCollectionAttribute)documentType
        .GetCustomAttributes(typeof(BsonCollectionAttribute), true)
        .FirstOrDefault())?.CollectionName;
    }
    #endregion
  }
}
