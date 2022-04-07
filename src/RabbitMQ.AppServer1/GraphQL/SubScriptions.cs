using HotChocolate.Execution;
using HotChocolate.Subscriptions;

namespace RabbitMQ.AppServer1.GraphQL;

[ExtendObjectType("Subscription")]  
public class BatchSubscription
{
  public const string SUBSCRIPTION_BATCH_CHANGED = "OnBatchChanged_{0}";
  public const string SUBSCRIPTION_BATCH_RECENT = "OnRecentBatches";

  public static string GetSubscriptionTopicName(string constant, string batchId)
  {
    return $"{constant}{batchId}";
  }

  [Subscribe]
  [Topic(SUBSCRIPTION_BATCH_RECENT)]
  public string OnRecentBatchesChanged(
    [EventMessage] string batchId,
    CancellationToken cancellationToken)
  {
    return batchId;
  }

  [Subscribe(With = nameof(SubscribeToOnBatchChangedAsync))]
  public string OnBatchChanged(
    string batchId,
    [EventMessage] string changedBatchId,
    CancellationToken cancellationToken)
  {
    return changedBatchId;
  }

  public async ValueTask<ISourceStream<string>> SubscribeToOnBatchChangedAsync(
    string batchId,
    [Service] ITopicEventReceiver eventReceiver,
    CancellationToken cancellationToken) =>
    await eventReceiver.SubscribeAsync<string, string>(
      $"{GetSubscriptionTopicName(SUBSCRIPTION_BATCH_CHANGED,batchId)}", cancellationToken);
}