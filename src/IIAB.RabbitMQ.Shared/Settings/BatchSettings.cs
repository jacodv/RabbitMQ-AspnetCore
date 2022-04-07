using IIAB.RabbitMQ.Shared.Models;
using RabbitMQ.Client;

namespace IIAB.RabbitMQ.Shared.Settings
{
  public static class BatchSettings
  {
    private const string BatchActionsExchange = "exch-batch-actions";
    private const string BatchActionsQueueName = "queue-batch-actions";

    private const string BatchProcessingExchange = "exch-batch-processing";
    private const string BatchProcessingQueueName = "queue-batch-processing-{0}";


    public static QueueSettings ForBatchActions()
    {
      return new QueueSettings()
      {
        ExchangeName = BatchActionsExchange,
        QueueName = BatchActionsQueueName,
        ExchangeType = ExchangeType.Fanout,
        TimeToLive = 600000
      };
    }

    public static QueueSettings ForBatchProcessing(string batchId)
    {
      return new QueueSettings()
      {
        ExchangeName = BatchProcessingExchange,
        QueueName = string.Format(BatchProcessingQueueName, batchId),
        ExchangeType = ExchangeType.Topic,
        TimeToLive = 600000
      };
    }

  }

  public class QueueSettings
  {
    public string ExchangeName { get; set; }
    public string ExchangeType { get; set; } = global::RabbitMQ.Client.ExchangeType.Topic;
    public string QueueName { get; set; }
    public int? TimeToLive { get; set; } = 600000;
  }

  public static class BatchRouteSettings
  {
    public const string AllActions = "batch-action.*";
    public const string StartAction = "batch-action-start";
    public const string CompletedAction = "batch-action-complete";
    public const string StageProcessing = "batch-processing-{0}.{1}";
    public const string AllStageProcessing = "batch-processing-{0}.*";
  }

  public static class SettingExtensions
  {
    public static RabbitClientSettings AsRabbitClientSettings(this QueueSettings input)
    {
      return new RabbitClientSettings()
      {
        ExchangeName = input.ExchangeName,
        ExchangeType = input.ExchangeType,
        TimeToLive = input.TimeToLive
      };
    }
    public static RabbitConsumerSettings AsRabbitConsumerSettings(this QueueSettings input, string routeKey)
    {
      return new RabbitConsumerSettings()
      {
        ExchangeName = input.ExchangeName,
        ExchangeType = input.ExchangeType,
        TimeToLive = input.TimeToLive,
        PreFetchCount = 5,
        QueueName = input.QueueName,
        RouteKey = routeKey
      };
    }
  }
}
