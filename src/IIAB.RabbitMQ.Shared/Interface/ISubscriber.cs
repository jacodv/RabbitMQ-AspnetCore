using IIAB.RabbitMQ.Shared.Models;
using RabbitMQ.Client;

namespace IIAB.RabbitMQ.Shared.Interface;

public interface IQueueSubscriber : IDisposable
{
  void Subscribe<T>(Func<T, IDictionary<string, object>, bool> callback);
  void SubscribeAsync<T>(Func<T?, IDictionary<string, object>, Task<bool>> callback);
}

public interface IQueuePublisher
{
  void Publish<T>(T message, string routingKey, IDictionary<string, object>? messageAttributes, int? timeToLive = null);
  void Publish<T>(IEnumerable<T> messages, string routingKey, IDictionary<string, object> messageAttributes, int? timeToLive = null);
}

public interface IConnectionProvider : IDisposable
{
  IConnection GetConnection();
}