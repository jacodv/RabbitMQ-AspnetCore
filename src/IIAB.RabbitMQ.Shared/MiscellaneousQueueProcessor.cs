using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Shared.Interface;
using RabbitMQ.Shared.Models;

namespace RabbitMQ.Shared
{
  public class MiscellaneousQueueProcessor : IQueueProcessor, IDisposable
  {
    private readonly ILogger _logger;
    private readonly IConnectionsProvider _connectionsProvider;

    public MiscellaneousQueueProcessor(
      ILogger logger,
      IConnectionsProvider connectionsProvider)
    {
      _logger = logger;
      _connectionsProvider = connectionsProvider;
    }

    private static readonly SemaphoreSlim _locker = new(1);
    private static readonly ConcurrentDictionary<string, int> _batchesProcessed = new();
    private static readonly ConcurrentDictionary<string, int> _batchExpectedCount = new();
    private static readonly ConcurrentDictionary<string, bool> _batchBatchCreated = new();
    private readonly ConcurrentDictionary<string, QueueSubscriber> _batchProcessors = new();

    #region IQueueProcessor
    public async Task ProcessMessage(QueueMessage<object> message, string serviceId)
    {
      switch (message.BodyType)
      {
        case QueueMessage<object>.BatchMessage:
          await _processBatchMessage(message, serviceId);
          break;
        default:
          _logger?.LogDebug($"Handling message [{serviceId}] \nMessage:{JsonSerializer.Serialize(message)}");
          break;
      }

    }
    #endregion

    #region Overrides of BackgroundService

    public void Dispose()
    {
      _logger?.LogInformation($"Disposing {nameof(MiscellaneousQueueProcessor)}");
      _cleanUp();
    }

    #endregion

    #region Private
    private async Task _processBatchMessage(QueueMessage<object> message, string serviceId)
    {
      var batchMessage = message.GetBodyAs<BatchMessage>();

      if (batchMessage!.IsFirst)
      {
        await _processFirstMessage(message, batchMessage, serviceId);
        return;
      }

      // ReSharper disable once InvertIf
      if (batchMessage!.IsLast)
      {
        _processCompletedBatch(message, batchMessage, serviceId);
        return;
      }

      throw new NotImplementedException($"Unsupported BatchMessage - Only first and last allowed: {serviceId}");
    }

    private void _processCompletedBatch(QueueMessage<object> message, BatchMessage batchMessage, string serviceId)
    {
      _logger.LogDebug($"Removing counters for: {message.LinkedId} - {serviceId}");
      _batchExpectedCount.TryRemove(message.LinkedId, out var removedExpected);
      var processedKeysToRemove = _batchesProcessed.Keys.Where(x => x.StartsWith(message.LinkedId)).ToList();
      foreach (var key in processedKeysToRemove)
      {
        _batchesProcessed.TryRemove(key, out var removedProcessed);
      }

      _logger.LogDebug($"Closing subscribers for: {message.LinkedId} - {serviceId}");
      var subscriberKeysToRemove = _batchProcessors.Keys.Where(x => x.StartsWith(message.LinkedId)).ToList();
      foreach (var key in subscriberKeysToRemove)
      {
        _batchProcessors.TryRemove(key, out var removedSubscriber);
        removedSubscriber?.Dispose();
      }
    }

    private Task _processFirstMessage(QueueMessage<object> message, BatchMessage batchMessage, string serviceId)
    {
      _batchesProcessed.TryAdd(_getBatchServiceId(message.LinkedId, serviceId), 0);
      _batchExpectedCount.TryAdd(message.LinkedId, batchMessage.ExpectedCount);

      var consumerSettings = new RabbitConsumerSettings()
      {
        ExchangeName = RabbitConsumerSettings.BATCH_EXCHANGE,
        ExchangeType = ExchangeType.Topic,
        PreFetchCount = 5,
        RouteKey = $"batch.{message.LinkedId}",
        QueueName = $"batch-queue.{message.LinkedId}"
      };

      var queueSubscriber1 = new QueueSubscriber(
        _connectionsProvider,
        _logger,
        consumerSettings,
        "AppServer",
        "001");
      queueSubscriber1.SubscribeAsync<QueueMessage<BatchMessage>>(_processItemMessage!);
      _batchProcessors.TryAdd(_getBatchServiceId(message.LinkedId, queueSubscriber1.SubscriberId), queueSubscriber1);

      var queueSubscriber2 = new QueueSubscriber(
        _connectionsProvider,
        _logger,
        consumerSettings,
        "AppServer",
        "002");
      queueSubscriber2.SubscribeAsync<QueueMessage<BatchMessage>>(_processItemMessage!);
      _batchProcessors.TryAdd(_getBatchServiceId(message.LinkedId, queueSubscriber2.SubscriberId), queueSubscriber2);

      return _createBatchItems(message, batchMessage, serviceId);
    }

    private async Task _createBatchItems(QueueMessage<object> message, BatchMessage batchMessage, string serviceId)
    {
      await _locker.WaitAsync();
      try
      {
        _batchBatchCreated.TryGetValue(message.LinkedId, out var started);
        if (started)
          return;

        var settings = new RabbitClientSettings()
        {
          ExchangeName = RabbitConsumerSettings.BATCH_EXCHANGE,
          ExchangeType = ExchangeType.Topic,
        };
        using var queuePublisher = new QueuePublisher(_connectionsProvider, _logger, settings);

        var messages = Enumerable.Range(1, batchMessage.ExpectedCount).Select(itemNo => new QueueMessage<BatchMessage>()
        {
          Id = $"{itemNo}|{Guid.NewGuid()}",
          LinkedId = message.LinkedId,
          BodyType = QueueMessage<object>.BatchMessage,
          Body = new BatchMessage()
          {
            ItemStage = 0,
            ExpectedCount = batchMessage.ExpectedCount
          }
        }).ToList();

        // Add the last message
        var lastMessage = new QueueMessage<BatchMessage>()
        {
          Id = "LastMessage",
          LinkedId = message.LinkedId,
          BodyType = QueueMessage<object>.BatchMessage,
          Body = new BatchMessage()
          {
            ItemStage = 0,
            ExpectedCount = batchMessage.ExpectedCount,
            IsLast = true
          }
        };
        messages.Add(lastMessage);

        queuePublisher.Publish(
          (IList<QueueMessage<BatchMessage>>)messages,
          $"batch.{message.LinkedId}",
          null);

        _batchBatchCreated.TryAdd(message.LinkedId, true);
        _logger.LogInformation($"Created [{batchMessage.ExpectedCount} for batch:{message.LinkedId} by {serviceId}");
      }
      finally
      {
        _locker.Release();
      }
    }

    private async Task<bool> _processItemMessage(QueueMessage<BatchMessage> message, string subscriberId, IDictionary<string, object> headers)
    {
      if (message.Body.IsLast)
      {
        return await _processLastMessage(message, subscriberId, headers);
      }
      await Task.Delay(100);
      _logger.LogDebug($"Completed BatchItem: {message.Id} from batch {message.LinkedId} by {subscriberId}");
      _batchesProcessed.AddOrUpdate(_getBatchServiceId(message.LinkedId, subscriberId), 1, (id, count) => count + 1);
      return true;
    }

    private async Task<bool> _processLastMessage(QueueMessage<BatchMessage> message, string subscriberId, IDictionary<string, object> headers)
    {
      _logger.LogDebug($"Processing last message for {message.LinkedId} by {subscriberId}");

      var expectedCount = _batchExpectedCount[message.LinkedId];
      var processedCount = _batchesProcessed.First(x => x.Key.StartsWith(message.LinkedId)).Value;

      var wait = DateTime.Now.AddMinutes(1);
      while (DateTime.Now < wait)
      {
        if (processedCount == expectedCount)
        {
          _logger.LogInformation($"Batch completed:[{message.LinkedId}]: {expectedCount} | {subscriberId}");
          await _sendBatchCompleted(message.LinkedId, expectedCount, subscriberId);
          return true;
        }

        await _sendBatchCompleted(message.LinkedId, processedCount, subscriberId);
        await Task.Delay(100);

        processedCount = _batchesProcessed.First(x => x.Key.StartsWith(message.LinkedId)).Value;
      }

      _logger.LogWarning($"Batch TIMEOUT: [{message.LinkedId}]: {processedCount} of {expectedCount} | {subscriberId}");
      throw new InvalidOperationException($"Batch TIMEOUT: [{message.LinkedId}]: {processedCount} of {expectedCount} | {subscriberId}");
    }

    private string _getBatchServiceId(string batchId, string serviceId)
    {
      return $"{batchId}|{serviceId}";
    }

    private void _cleanUp()
    {
      if (_batchProcessors?.Any() == true)
      {
        foreach (var queueSubscriber in _batchProcessors.Values)
        {
          queueSubscriber?.Dispose();
        }
      }
      _batchProcessors?.Clear();
    }

    private async Task _sendBatchCompleted(string batchId, int expectedCount, string subscriberId)
    {
      try
      {
        await _locker.WaitAsync();
        if (!_batchBatchCreated.ContainsKey(batchId))
          return;

        var settings = new RabbitClientSettings()
        {
          ExchangeName = RabbitConsumerSettings.MISC_ECHANGE,
          ExchangeType = ExchangeType.Fanout,
        };
        using var queuePublisher = new QueuePublisher(_connectionsProvider, _logger, settings);

        var message = new QueueMessage<BatchMessage>()
        {
          LinkedId = batchId,
          BodyType = QueueMessage<object>.BatchMessage,
          Body = new BatchMessage()
          {
            ExpectedCount = expectedCount,
            IsLast = true
          }
        };

        queuePublisher.Publish(message, "misc.batchCompleted", null);
        _batchBatchCreated.TryRemove(batchId, out var removed);
        _logger.LogInformation($"Sent Batch[{batchId}] Completed message by {subscriberId}");
      }
      finally
      {
        _locker.Release();
      }

    }
    #endregion
  }
}
