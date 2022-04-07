using System.Text.Json;
using HotChocolate.Subscriptions;
using IIAB.RabbitMQ.Shared.Interface;
using IIAB.RabbitMQ.Shared.Models;
using IIAB.RabbitMQ.Shared.Settings;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using RabbitMQ.Models;
using RabbitMQ.Models.Enums;
using UtilityData.Data.Interfaces;

namespace IIAB.RabbitMQ.Shared;

public class BatchItemMessageProcessor : IDisposable
{
  private readonly IConnectionProvider _connectionProvider;
  private readonly ILogger _logger;
  private readonly IRepository<Batch> _batchRepository;
  private readonly IRepository<BatchItem> _batchItemRepository;
  private readonly IBatchMessageSender _batchMessageSender;
  private readonly ITopicEventSender _eventSender;
  private readonly string _applicationName;
  private readonly string _subscriberTag;
  private readonly QueueSubscriber _queueSubscriber;

  public BatchItemMessageProcessor(
    IConnectionProvider connectionProvider,
    ILogger logger,
    IRepository<Batch> batchRepository,
    IRepository<BatchItem> batchItemRepository,
    IBatchMessageSender batchMessageSender,
    ITopicEventSender eventSender,
    string batchId,
    string applicationName,
    string subscriberTag)
  {
    _connectionProvider = connectionProvider;
    _logger = logger;
    _batchRepository = batchRepository;
    _batchItemRepository = batchItemRepository;
    _batchMessageSender = batchMessageSender;
    _eventSender = eventSender;
    _applicationName = applicationName;
    _subscriberTag = subscriberTag;
    var settings = BatchSettings
      .ForBatchProcessing(batchId)
      .AsRabbitConsumerSettings(string.Format(BatchRouteSettings.AllStageProcessing, batchId));

    _queueSubscriber = new QueueSubscriber(
      connectionProvider,
      logger,
      settings,
      applicationName,
      subscriberTag
    );

    _queueSubscriber.SubscribeAsync<QueueMessage<object>>(_processBatchMessage!);
    _logger.LogDebug($"Constructed {nameof(BatchItemMessageProcessor)} - {_getSubscriber()}");
  }

  #region Private

  private async Task<bool> _processBatchMessage(QueueMessage<object> message, string subscriberId, IDictionary<string,object> headers)
  {
    var batchMessage = message.GetBodyAs<BatchMessage>();

    // ReSharper disable once InvertIf
    if (batchMessage!.IsLast)
    {
      await _processLastMessage(message, batchMessage, subscriberId);
      return true;
    }

    return  await _processBatchItem(message, batchMessage);
  }
  private async Task<bool> _processBatchItem(QueueMessage<object> message, BatchMessage batchMessage)
  {
    var stage = (BatchStage)batchMessage.ItemStage;

    // Simulate some work done
    await Task.Delay(100);

    await _batchItemRepository.UpdateById(message.Id, Builders<BatchItem>.Update.Set(x => x.Processed, true));
    await _batchRepository.UpdateById(message.LinkedId, Builders<Batch>.Update.Inc(x => x.Stages[stage.ToString()], 1));
    
    _logger.LogDebug($"Processed BatchItem: {message.Id}|{JsonSerializer.Serialize(message.Body)} of {message.LinkedId} - {_getSubscriber()}");
    
    await _eventSender.SendAsync("OnRecentBatches", $"{message.Id}-{DateTime.Now.ToLongTimeString()}");

    return true;
  }
  private async Task _processLastMessage(QueueMessage<object> message, BatchMessage batchMessage, string serviceId)
  {
    var stage = (BatchStage)batchMessage.ItemStage;
    var waiting = true;
    var timeToWait = DateTime.Now.AddMinutes(5);

    var batch = await _batchRepository.FindByIdAsync(message.LinkedId);

    _logger.LogDebug($"Processing LastMessage for {message.LinkedId} - {_getSubscriber()}");

    while (waiting)
    {
      if (batch.Stages[stage.ToString()] == batch.ItemCount)
        break;
      if (DateTime.Now > timeToWait)
        throw new InvalidOperationException($"Batch processing timeout: Processed: {batch.Stages[stage.ToString()]}");

      await Task.Delay(1000);
      batch = await _batchRepository.FindByIdAsync(message.LinkedId);
    }

    _batchMessageSender.SendBatchActionMessage(batch.Id, BatchRouteSettings.CompletedAction);

    _logger.LogInformation($"Sent Completed processing message for {message.LinkedId} - {_getSubscriber()}");

  }
  private string _getSubscriber()
  {
    return $"{_applicationName}-{_subscriberTag}";
  }

  #endregion

  #region IDisposable

  public void Dispose()
  {
    _logger.LogDebug($"Disposing {nameof(BatchItemMessageProcessor)} - {_getSubscriber()}");
    _queueSubscriber.Dispose();
  }

  #endregion
}