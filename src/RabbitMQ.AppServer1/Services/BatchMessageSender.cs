using RabbitMQ.Models.Enums;
using RabbitMQ.Shared;
using RabbitMQ.Shared.Interface;
using RabbitMQ.Shared.Models;
using RabbitMQ.Shared.Settings;

namespace RabbitMQ.AppServer1.Services
{
  public class BatchMessageSender: IBatchMessageSender
  {
    private readonly IConnectionProvider _connectionProvider;
    private readonly ILogger<BatchMessageSender> _logger;

    public BatchMessageSender(
      IConnectionProvider connectionProvider,
      ILogger<BatchMessageSender> logger)
    {
      _connectionProvider = connectionProvider;
      _logger = logger;
    }

    public void SendBatchItemMessages<T>(IList<T> messages, string batchId, BatchStage stage)
    {
      var settings = BatchSettings.ForBatchProcessing(batchId).AsRabbitClientSettings();

      using var queuePublisher = new QueuePublisher(
        _connectionProvider,
        _logger,
        settings);

      queuePublisher.Publish(
        (IList<QueueMessage<BatchMessage>>)messages, 
        string.Format(BatchRouteSettings.StageProcessing, batchId, stage),
        null);
    }

    public void SendBatchActionMessage(string batchId, string action, string? status=null)
    {
      using var queuePublisher = new QueuePublisher(
        _connectionProvider,
        _logger,
        BatchSettings.ForBatchActions().AsRabbitClientSettings());

      var actionMessage = new QueueMessage<string>()
      {
        Id = batchId,
        BodyType = nameof(String),
        Body = status==null?action:$"{action}-{status}"
      };

      queuePublisher.Publish(
        actionMessage, 
        action,
        null);
    }
  }
}
