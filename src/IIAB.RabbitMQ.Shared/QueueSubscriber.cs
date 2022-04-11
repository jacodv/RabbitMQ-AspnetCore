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

    public QueueSubscriber(
        IConnectionProvider connectionProvider,
        ILogger logger,
        RabbitConsumerSettings settings,
        string applicationName,
        string tag)
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
      var consumer = new EventingBasicConsumer(_model);
      consumer.Received += (sender, e) =>
      {
        var messageObject = _getMessageAsInstance<T>(e);
        var success = callback.Invoke(messageObject!, _subscriberId, e.BasicProperties.Headers);
        if (success)
        {
          _model.BasicAck(e.DeliveryTag, true);
        }
      };

      _model.BasicConsume(_queueName, false, consumer);
    }

    public void SubscribeAsync<T>(Func<T?, string, IDictionary<string, object>, Task<bool>> callback)
    {
      var consumer = new AsyncEventingBasicConsumer(_model);
      consumer.Received += async (sender, e) =>
      {
        var body = e.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);
        var messageObject = JsonSerializer.Deserialize<T>(message);

        var success = await callback.Invoke(messageObject, _subscriberId, e.BasicProperties.Headers);
        if (success)
        {
          _model.BasicAck(e.DeliveryTag, true);
        }
      };

      _model.BasicConsume(_queueName, false, consumer);
    }

    public override void Dispose()
    {
      Dispose(true);
      GC.SuppressFinalize(this);
    }

    public string SubscriberId => _subscriberId;

    #region Private

    private T? _getMessageAsInstance<T>(BasicDeliverEventArgs e)
    {
      var body = e.Body.ToArray();
      var message = Encoding.UTF8.GetString(body);
      return JsonSerializer.Deserialize<T>(message);
    }

    private void _model_FlowControl(object? sender, FlowControlEventArgs e)
    {
      _logger.LogWarning($"RabbitMQ model flow control: {e.Active}");
    }

    private void _model_ModelShutdown(object? sender, ShutdownEventArgs e)
    {
      _logger.LogInformation($"RabbitMQ model shutdown: {_queueName} | {e.ReplyText}");
    }

    private void _model_CallbackException(object? sender, CallbackExceptionEventArgs e)
    {
      var detail = $"Detail:{e.Detail.Count}";
      try
      {
        detail = JsonSerializer.Serialize(e.Detail);
      }
      catch{}

      _logger.LogError($"RabbitMQ model callback exception: {_queueName}-{e.Exception?.Message}\n{detail}", e.Exception);
    }

    private void _model_BasicRecoverOk(object? sender, EventArgs e)
    {
      _logger.LogInformation($"RabbitMQ model recover OK: {_queueName}");
    }

    #endregion

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
  }
}
