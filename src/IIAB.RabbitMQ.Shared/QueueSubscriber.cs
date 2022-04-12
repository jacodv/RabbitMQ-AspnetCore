using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Shared.Interface;
using RabbitMQ.Shared.Models;

namespace RabbitMQ.Shared
{
  public sealed class QueueSubscriber : QueueBase, IQueueSubscriber
  {
    private readonly ILogger _logger;
    private readonly IModel _model;
    private bool _disposed;
    private readonly string _queueName;
    private readonly string _subscriberId;
    private string _consumerTag = null!;

    public QueueSubscriber(
        IConnectionProvider connectionProvider,
        ILogger logger,
        RabbitConsumerSettings settings,
        string applicationName,
        string tag,
        CancellationTokenSource? cancellationTokenSource = null):
      base(cancellationTokenSource)
    {
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
      _model = connectionProvider!.GetConsumerConnection().CreateModel();
      _model.BasicRecoverOk += _model_BasicRecoverOk;
      _model.CallbackException += _model_CallbackException;
      _model.ModelShutdown += _model_ModelShutdown;
      _model.FlowControl += _model_FlowControl;

      ConfigureExchange(_model, settings);

      var uniqueId = Guid.NewGuid().ToString("N");
      _subscriberId = $"{applicationName}-{tag}-{uniqueId}";
      _queueName = settings.ExchangeType==ExchangeType.Fanout? 
        $"{settings.QueueName}-{uniqueId}":
        settings.QueueName;

      ConfigureQueue(_model, settings, _queueName);

      _logger.LogDebug($"Started Queue Subscriber:{_subscriberId}\n{JsonSerializer.Serialize(settings)}");
    }

    public void Subscribe<T>(Func<T, string, IDictionary<string, object>, bool> callback)
    {
      if (!string.IsNullOrEmpty(_consumerTag))
        throw new InvalidOperationException($"Subscribe has already been started for: {_queueName}|{_subscriberId}");

      var consumer = new EventingBasicConsumer(_model);
      consumer.Received += (sender, e) =>
      {
        if (_cancellationToken.IsCancellationRequested)
        {
          _logger.LogWarning($"Subscription[{_queueName}|{_subscriberId}] cancelled, Message not processed.");
          return;
        }

        var messageObject = _getMessageAsInstance<T>(e);
        try
        {
          var success = callback.Invoke(messageObject!, _subscriberId, e.BasicProperties.Headers);
          if (success)
            _model.BasicAck(e.DeliveryTag, false);
          else
            _model.BasicReject(e.DeliveryTag, true);

        }
        catch(Exception ex)
        {
          _logger.LogError(ex, $"Message not processed: {ex.Message}\n{JsonSerializer.Serialize(messageObject)}");
          _model.BasicReject(e.DeliveryTag, false);
        }
      };
      _consumerTag = _model.BasicConsume(_queueName, false, consumer);
    }

    public void SubscribeAsync<T>(Func<T?, string, IDictionary<string, object>, Task<bool>> callback)
    {
      if (!string.IsNullOrEmpty(_consumerTag))
        throw new InvalidOperationException($"SubscribeAsync has already been started for: {_queueName}|{_subscriberId}");

      var consumer = new AsyncEventingBasicConsumer(_model);
      consumer.Received += async (sender, e) =>
      {
        if (_cancellationToken.IsCancellationRequested)
        {
          _logger.LogWarning($"Subscription[{_queueName}|{_subscriberId}] cancelled, Message not processed");
          return;
        }

        var messageObject = _getMessageAsInstance<T>(e);

        try
        {
          var success = await callback.Invoke(messageObject, _subscriberId, e.BasicProperties.Headers);
          
          
          if (success)
            _model.BasicAck(e.DeliveryTag, false);
          else
            _model.BasicReject(e.DeliveryTag, true);

        }
        catch(Exception ex)
        {
          _logger.LogError(ex, $"Message not processed: {ex.Message}\n{JsonSerializer.Serialize(messageObject)}");
          _model.BasicReject(e.DeliveryTag, false);
        }

        await Task.Yield();
      };
      _consumerTag = _model.BasicConsume(_queueName, false, consumer);
    }

    public void Cancel(bool close)
    {
      _logger.LogInformation($"Cancelling[{_getLogLine()}]");
      _model?.BasicCancelNoWait(_consumerTag);

      if (_cancellationToken.CanBeCanceled)
        _cancellationTokenSource.Cancel();

      if(close)
        _model?.Close();
    }

    public string SubscriberId => _subscriberId;

    #region Disposing pattern
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

    ~QueueSubscriber()
    {
      Dispose(false);
    }
    #endregion

    #region Private

    private T? _getMessageAsInstance<T>(BasicDeliverEventArgs e)
    {
      var body = e.Body.ToArray();
      var message = Encoding.UTF8.GetString(body);
      return JsonSerializer.Deserialize<T>(message);
    }

    private void _model_FlowControl(object? sender, FlowControlEventArgs e)
    {
      _logger.LogWarning($"RabbitMQ model flow control: {e.Active}: {_getLogLine()}");
    }

    private void _model_ModelShutdown(object? sender, ShutdownEventArgs e)
    {
      _logger.LogInformation($"RabbitMQ model shutdown: {_getLogLine()} | {e.ReplyText}");
    }

    private void _model_CallbackException(object? sender, CallbackExceptionEventArgs e)
    {
      var detail = $"Detail:{e.Detail.Count}";
      try
      {
        detail = JsonSerializer.Serialize(e.Detail);
      }
      catch{}

      _logger.LogError($"RabbitMQ model callback exception: {_getLogLine()}-{e.Exception?.Message}\n{detail}", e.Exception);
    }

    private void _model_BasicRecoverOk(object? sender, EventArgs e)
    {
      _logger.LogInformation($"RabbitMQ model recover OK: {_getLogLine()}");
    }

    private string _getLogLine()
    {
      return $"{_queueName}|{_subscriberId}|{_consumerTag}";
    }
    #endregion

  }
}
