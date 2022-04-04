using System.Runtime.InteropServices.ObjectiveC;
using System.Text;
using System.Text.Json;
using IIAB.RabbitMQ.Shared.Interface;
using IIAB.RabbitMQ.Shared.Models;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace IIAB.RabbitMQ.Shared;

public sealed class QueuePublisher : IQueuePublisher, IDisposable
{
  private readonly ILogger _logger;
  private readonly RabbitClientSettings _rabbitClientSettings;
  private readonly IModel _model;
  private bool _disposed;

  public QueuePublisher(IConnectionProvider connectionProvider, ILogger logger, RabbitClientSettings rabbitClientSettings)
  {
    _logger = logger;
    _rabbitClientSettings = rabbitClientSettings;
    _model = connectionProvider.GetProducerConnection().CreateModel();
    var ttl = new Dictionary<string, object>
      {
        {"x-message-ttl", rabbitClientSettings.TimeToLive ?? TimeSpan.FromDays(1).Milliseconds }
      };
    _model.ExchangeDeclare(rabbitClientSettings.ExchangeName, rabbitClientSettings.ExchangeType, arguments: ttl);
  }

  public void Publish<T>(T message, string routingKey, IDictionary<string, object>? messageAttributes, int? timeToLive = null)
  {
    Publish(new[] { message } as IEnumerable<T>, routingKey, messageAttributes, timeToLive);
  }
  public void Publish<T>(IEnumerable<T> messages, string routingKey, IDictionary<string, object>? messageAttributes, int? timeToLive = null)
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

      _model.BasicPublish(_rabbitClientSettings.ExchangeName, routingKey, properties, body);
    }
  }

  public void Dispose()
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