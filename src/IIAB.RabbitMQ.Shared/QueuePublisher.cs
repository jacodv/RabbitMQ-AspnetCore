using System.Runtime.InteropServices.ObjectiveC;
using System.Text;
using System.Text.Json;
using IIAB.RabbitMQ.Shared.Interface;
using RabbitMQ.Client;

namespace IIAB.RabbitMQ.Shared;

public class QueuePublisher : IQueuePublisher, IDisposable
{
  private readonly IConnectionProvider _connectionProvider;
  private readonly string _exchange;
  private readonly IModel _model;
  private bool _disposed;

  public QueuePublisher(IConnectionProvider connectionProvider, string exchange, string exchangeType, int? timeToLive = 0)
  {
    _connectionProvider = connectionProvider;
    _exchange = exchange;
    _model = _connectionProvider.GetConnection().CreateModel();
    var ttl = new Dictionary<string, object>
      {
        {"x-message-ttl", timeToLive ?? TimeSpan.FromDays(1).Milliseconds }
      };
    _model.ExchangeDeclare(_exchange, exchangeType, arguments: ttl);
  }

  public void Publish<T>(T message, string routingKey, IDictionary<string, object>? messageAttributes, int? timeToLive = null)
  {
    Publish(new[] { message }, routingKey, messageAttributes, timeToLive);
  }
  public void Publish<T>(IEnumerable<T> messages, string routingKey, IDictionary<string, object> messageAttributes, int? timeToLive = null)
  {
    foreach (var message in messages)
    {
      var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message));
      var properties = _model.CreateBasicProperties();
      properties.Persistent = true;
      if (messageAttributes != null)
        properties.Headers = messageAttributes;
      if (timeToLive.HasValue)
        properties.Expiration = timeToLive.Value.ToString();

      _model.BasicPublish(_exchange, routingKey, properties, body);
    }
  }

  public void Dispose()
  {
    Dispose(true);
    GC.SuppressFinalize(this);
  }

  // Protected implementation of Dispose pattern.
  protected virtual void Dispose(bool disposing)
  {
    if (_disposed)
      return;

    if (disposing)
      _model?.Close();

    _disposed = true;
  }
}