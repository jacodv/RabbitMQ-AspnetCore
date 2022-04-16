﻿using System.ComponentModel;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Shared.Interface;
using RabbitMQ.Shared.Models;

namespace RabbitMQ.Shared;

public sealed class QueuePublisher : QueueBase, IQueuePublisher
{
  private readonly ILogger _logger;
  private readonly RabbitClientSettings _rabbitClientSettings;
  private readonly IModel _model;
  private bool _disposed;

  public QueuePublisher(IConnectionsProvider connectionsProvider, ILogger logger, RabbitClientSettings rabbitClientSettings, CancellationTokenSource? cancellationTokenSource = null):
    base(cancellationTokenSource)
  {
    _logger = logger;
    _rabbitClientSettings = rabbitClientSettings;
    _model = connectionsProvider.GetProducerConnection().CreateModel();

    ConfigureExchange(_model, rabbitClientSettings);
  }

  public void Publish<T>(T message, string routingKey, IDictionary<string, object>? messageAttributes, int? timeToLive = null)
  {
    Publish(new[] { message } as IList<T>, routingKey, messageAttributes, timeToLive);
  }

  public void Publish<T>(IList<T> messages, string routingKey, IDictionary<string, object>? messageAttributes, int? timeToLive = null)
  {
    var enableTransaction = messages.Count>1;
      
    try
    {
      if(enableTransaction)
        _model.TxSelect();
      
      foreach (var message in messages)
      {
        if (_cancellationToken.IsCancellationRequested)
        {
          if(enableTransaction)
            _model.TxRollback();
          _logger.LogWarning($"Publish[{_rabbitClientSettings.ExchangeName}|{_rabbitClientSettings.ExchangeType}] cancelled");
          return;
        }
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
        var properties = _model.CreateBasicProperties();
        properties.Persistent = true;
        if (messageAttributes != null)
          properties.Headers = messageAttributes;
        if (timeToLive.HasValue)
          properties.Expiration = timeToLive.Value.ToString();

        _model.BasicPublish(_rabbitClientSettings.ExchangeName, routingKey, properties, body);
      }
      if(enableTransaction)
        _model.TxCommit();
    }
    catch (Exception e)
    {
      _logger.LogError($"Failed to publish {messages?.Count()} - {e.Message}");
      if(enableTransaction)
        _model.TxRollback();
      throw;
    }
  }

  public void Cancel(bool close)
  {
    if(_cancellationToken.CanBeCanceled)
      _cancellationTokenSource.Cancel();
    if(close)
      Dispose();
  }

  public override void Dispose()
  {
    Dispose(true);
    GC.SuppressFinalize(this);
  }

  // Protected implementation of Dispose pattern.
  private void Dispose(bool disposing)
  {
    if (_disposed)
      return;

    if (disposing)
      _model?.Close();

    _disposed = true;
  }
}