using RabbitMQ.Models;
using UtilityData.Data.Interfaces;

namespace RabbitMQ.AppServer1.GraphQL;

[ExtendObjectType("Query")]
public class BatchQueries
{
  [UsePaging]
  [UseFiltering()]
  [UseSorting()]
  public IQueryable<Batch> GetBatches(
    [Service] IRepository<Batch> repo)
  {
    try
    {
      return repo.AsQueryable();
    }
    catch (Exception e)
    {
      Console.WriteLine(e);
      throw;
    }
  }

  public Task<Batch> GetBatchById(
    string id,
    [Service] IRepository<Batch> repo)
  {
    return repo.FindByIdAsync(id);
  }
}

[ExtendObjectType("Query")]
public class BatchItemQueries
{
  [UsePaging]
  [UseFiltering()]
  [UseSorting()]
  public IQueryable<BatchItem> GetBatchItems(
    [Service] IRepository<BatchItem> repo)
  {
    return repo.AsQueryable();
  }

  public Task<BatchItem> GetBatchItemById(
    string id,
    [Service] IRepository<BatchItem> repo)

  {
    return repo.FindByIdAsync(id);
  }
}

public class BatchModelType:ObjectType<Batch>
{
  #region Overrides of ObjectType<Batch>

  protected override void Configure(IObjectTypeDescriptor<Batch> descriptor)
  {
    descriptor.Field(x => x.StageFlags).Ignore();
    descriptor.Field(x => x.GetNextStage).Ignore();
    descriptor.Field(x => x.IsCompleted).Ignore();
  }

  #endregion
}