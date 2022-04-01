using System.Text;
using System.Text.Json;
using IIAB.RabbitMQ.Shared.Interface;
using IIAB.RabbitMQ.Shared.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace IIAB.RabbitMQ.Shared
{
  public class QueueSubscriber : IQueueSubscriber
  {
    private readonly IConnectionProvider _connectionProvider;
    private readonly string _exchange;
    private readonly string _queue;
    private readonly IModel _model;
    private bool _disposed;

    public QueueSubscriber(
        IConnectionProvider connectionProvider,
        string exchange,
        string queue,
        string routingKey,
        string exchangeType,
        int? timeToLive = 30000,
        ushort prefetchSize = 10)
    {
      _connectionProvider = connectionProvider;
      _exchange = exchange;
      _queue = queue;
      _model = _connectionProvider.GetConnection().CreateModel();

      var ttl = new Dictionary<string, object>
        {
          {"x-message-ttl", timeToLive ?? TimeSpan.FromDays(1).Milliseconds}
        };

      _model.ExchangeDeclare(_exchange, exchangeType, arguments: ttl);
      _model.QueueDeclare(_queue,
          durable: true,
          exclusive: false,
          autoDelete: false,
          arguments: null);
      _model.QueueBind(_queue, _exchange, routingKey);
      _model.BasicQos(0, prefetchSize, false);
    }

    public void Subscribe<T>(Func<T, IDictionary<string, object>, bool> callback)
    {
      var consumer = new EventingBasicConsumer(_model);
      consumer.Received += (sender, e) =>
      {
        var messageObject = _getMessageAsInstance<T>(e);
        bool success = callback.Invoke(messageObject, e.BasicProperties.Headers);
        if (success)
        {
          _model.BasicAck(e.DeliveryTag, true);
        }
      };

      _model.BasicConsume(_queue, false, consumer);
    }

    public void SubscribeAsync<T>(Func<T?, IDictionary<string, object>, Task<bool>> callback)
    {
      var consumer = new AsyncEventingBasicConsumer(_model);
      consumer.Received += async (sender, e) =>
      {
        var body = e.Body.ToArray();
        var message = Encoding.UTF8.GetString(body);
        var messageObject = JsonSerializer.Deserialize<T>(message);

        bool success = await callback.Invoke(messageObject, e.BasicProperties.Headers);
        if (success)
        {
          _model.BasicAck(e.DeliveryTag, true);
        }
      };

      _model.BasicConsume(_queue, false, consumer);
    }

    public void Dispose()
    {
      Dispose(true);
      GC.SuppressFinalize(this);
    }

    #region Private

    private T? _getMessageAsInstance<T>(BasicDeliverEventArgs e)
    {
      var body = e.Body.ToArray();
      var message = Encoding.UTF8.GetString(body);
      return JsonSerializer.Deserialize<T>(message);
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
