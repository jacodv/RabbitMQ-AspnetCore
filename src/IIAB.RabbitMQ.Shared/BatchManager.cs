﻿using System.Collections.Concurrent;
using System.Text.Json;
using IIAB.RabbitMQ.Shared.Interface;
using IIAB.RabbitMQ.Shared.Models;
using IIAB.RabbitMQ.Shared.Settings;
using Microsoft.Extensions.Logging;
using RabbitMQ.Models;
using RabbitMQ.Models.Enums;
using UtilityData.Data.Interfaces;

namespace IIAB.RabbitMQ.Shared;

public class BatchManager: IBatchManager, IDisposable
{
  private readonly ILogger<BatchManager> _logger;
  private readonly IConnectionProvider _connectionProvider;
  private readonly IRepository<Batch> _batchRepository;
  private readonly IRepository<BatchItem> _itemRepository;
  private readonly IBatchMessageSender _batchMessageSender;
  private readonly string _applicationName;
  private readonly string _subscriberTag;
  private readonly ConcurrentDictionary<string, BatchItemMessageProcessor> _batchMessageProcessors;

  public BatchManager(
    IConnectionProvider connectionProvider,
    ILogger<BatchManager> logger, 
    IRepository<Batch> batchRepository,
    IRepository<BatchItem> itemRepository,
    IBatchMessageSender batchMessageSender,
    string applicationName,
    string subscriberTag)
  {
    _logger = logger;
    _connectionProvider = connectionProvider;
    _batchRepository = batchRepository;
    _itemRepository = itemRepository;
    _batchMessageSender = batchMessageSender;
    _applicationName = applicationName;
    _subscriberTag = subscriberTag;

    _batchMessageProcessors = new();
  }


  public async Task<Batch> CreateBatch(NewBatch newBatch)
  {
    var batch = new Batch(newBatch.Name)
    {
      ItemCount = newBatch.ItemCount,
      StageFlags = newBatch.Stages,
    };

    foreach (Enum value in Enum.GetValues(newBatch.Stages.GetType()))
      if (newBatch.Stages.HasFlag(value))
        batch.Stages.Add(value.ToString(), 0);

    await _batchRepository.InsertOneAsync(batch);

    var itemsToInsert = Enumerable.Range(1, newBatch.ItemCount).Select(itemNo =>
    {
      var batchItem = new BatchItem($"{newBatch.Name}-{itemNo}", itemNo);
      batchItem.Batch = new Reference(batch.Id, batch.Name);
      return batchItem;
    }).ToList();

    await _itemRepository.InsertManyAsync(itemsToInsert);

    _batchMessageSender.SendBatchActionMessage(batch.Id, BatchRouteSettings.StartAction);

    return batch;
  }

  public void PublishStageMessages(string batchId, BatchStage stage)
  {
    _logger.LogDebug($"Creating batch processing messages for: {batchId}");

    var batchItems = _itemRepository.AsQueryable()
      .Where(x => x.Batch.Id == batchId)
      .ToList()
      .Select(item => new QueueMessage<BatchMessage>()
      {
        Id = item.Id,
        LinkedId = batchId,
        BodyType = nameof(Reference),
        Body = new BatchMessage()
        {
          ItemStage = (int)stage,
          Reference = new Reference(item.Id, item.Name)
        }
      })
      .ToList();

    // Add the last message
    batchItems.Add(new QueueMessage<BatchMessage>()
    {
      LinkedId = batchId,
      BodyType = nameof(Reference),
      Body = new BatchMessage()
      {
        ItemStage = (int)stage,
        IsLast = true
      }
    });

    _logger.LogInformation($"Publishing {batchItems.Count} message to the processing queue for: {batchId}");

    _batchMessageSender.SendBatchItemMessages(batchItems, batchId, stage);
  }

  public async Task<bool> ProcessBatchAction(QueueMessage<object> message)
  {
    if (message == null) throw new ArgumentNullException(nameof(message));

    switch (message.Body.ToString())
    {
      case BatchRouteSettings.StartAction:
        
        _createBatchProcessingSubscriber(message.Id);
        
        var batch = await _batchRepository.FindByIdAsync(message.Id);
        var firstStage = Enum.Parse<BatchStage>(batch.Stages.Keys.First());
        
        _logger.LogDebug($"Waiting for all subscribers to be registered for: {message.Id}");

        await Task.Delay(1000); // Allow subscribers to be registered for the batch
        
        PublishStageMessages(message.Id, firstStage);
        break;
      case BatchRouteSettings.CompletedAction:
        await _handleCompleteMessage(message);
        break;
      default:
        throw new ArgumentOutOfRangeException(nameof(message), $"Unsupported action: {message.Body}");
    }

    return true;
  }

  #region Private
  private void _createBatchProcessingSubscriber(string batchId)
  {
    if (_batchMessageProcessors.ContainsKey(batchId))
      throw new InvalidOperationException($"The batch: {batchId} has already been registered");

    var itemMessageProcessor = new BatchItemMessageProcessor(
      _connectionProvider,
      _logger,
      _batchRepository,
      _itemRepository,
      _batchMessageSender,
      batchId,
      _applicationName,
      _subscriberTag);

    _batchMessageProcessors.TryAdd(batchId, itemMessageProcessor);
  }
  private async Task _handleCompleteMessage(QueueMessage<object> message)
  {
    var batch = await _batchRepository.FindByIdAsync(message.Id);

    _logger.LogDebug($"Complete message for batch: {message.Id}\n{JsonSerializer.Serialize(batch)}");

    if (batch.IsCompleted())
    {
      _removeBatchProcessingSubscriber(message.Id);
      return;
    }

    var nextStage = batch.GetNextStage();
    _logger.LogDebug($"Starting next stage {nextStage} for: {message.Id}");

    PublishStageMessages(message.Id, nextStage);
  }
  private void _removeBatchProcessingSubscriber(string batchId)
  {
    _logger.LogInformation($"Removing {nameof(BatchItemMessageProcessor)} for {batchId}");
    _batchMessageProcessors.TryRemove(batchId, out var processorToRemove);
    processorToRemove?.Dispose();
  }
  #endregion

  #region IDisposable

  public void Dispose()
  {
    if (_batchMessageProcessors?.Any() != true) 
      return;
    
    var keysToRemove = _batchMessageProcessors.Keys;
    foreach (var key in keysToRemove)
    {
      _batchMessageProcessors.TryRemove(key, out var itemToRemove);
      itemToRemove?.Dispose();
    }
  }

  #endregion
}