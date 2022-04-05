using System.Text;
using System.Text.Json;
using IIAB.RabbitMQ.Shared.Interface;
using IIAB.RabbitMQ.Shared.Models;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace IIAB.RabbitMQ.Shared
{
  public class QueueSubscriber : IQueueSubscriber
  {
    private readonly IConnectionProvider? _connectionProvider;
    private readonly ILogger _logger;
    private readonly RabbitConsumerSettings _settings;
    private readonly IModel _model;
    private bool _disposed;
    private readonly string _queueName;
    private string _subscriberId = $"{Guid.NewGuid():N}";

    public QueueSubscriber(
        IConnectionProvider connectionProvider,
        ILogger logger,
        RabbitConsumerSettings settings)
    {
      _connectionProvider = connectionProvider ?? throw new ArgumentNullException(nameof(connectionProvider));
      _logger = logger;
      _settings = settings;
      _model = _connectionProvider!.GetConsumerConnection().CreateModel();
      _model.BasicRecoverOk += _model_BasicRecoverOk;
      _model.CallbackException += _model_CallbackException;
      _model.ModelShutdown += _model_ModelShutdown;
      _model.FlowControl += _model_FlowControl;


      var ttl = new Dictionary<string, object>
        {
          {"x-message-ttl", _settings.TimeToLive ?? TimeSpan.FromDays(1).Milliseconds}
        };

      _model.ExchangeDeclare(_settings.ExchangeName, _settings.ExchangeType, arguments: ttl);

      _queueName = $"{_settings.QueueName}-{_subscriberId}";
      
      _model.QueueDeclare(_queueName,
          durable: true,
          exclusive: false,
          autoDelete: true,
          arguments: null);
      _model.QueueBind(_queueName, _settings.ExchangeName, _settings.RouteKey);
      _model.BasicQos(0, _settings.PreFetchCount, false);
    }

    public void Subscribe<T>(Func<T, string, IDictionary<string, object>, bool> callback)
    {
      var consumer = new EventingBasicConsumer(_model);
      consumer.Received += (sender, e) =>
      {
        var messageObject = _getMessageAsInstance<T>(e);
        bool success = callback.Invoke(messageObject, _subscriberId, e.BasicProperties.Headers);
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

        bool success = await callback.Invoke(messageObject, _subscriberId, e.BasicProperties.Headers);
        if (success)
        {
          _model.BasicAck(e.DeliveryTag, true);
        }
      };

      _model.BasicConsume(_queueName, false, consumer);
    }

    public void Dispose()
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
      _logger.LogError($"RabbitMQ model callback exception: {_queueName}-{e.Exception?.Message}\n{JsonSerializer.Serialize(e.Detail)}", e.Exception);
    }

    private void _model_BasicRecoverOk(object? sender, EventArgs e)
    {
      _logger.LogInformation($"RabbitMQ model recover OK: {_queueName}");
    }

    #endregion

    // Protected implementation of Dispose pattern.
    protected virtual void Dispose(bool disposing)
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
