using IIAB.RabbitMQ.Shared.Models;
using RabbitMQ.Client;

namespace IIAB.RabbitMQ.Shared.Interface;

public interface IQueueSubscriber : IDisposable
{
  void Subscribe<T>(Func<T, string, IDictionary<string, object>, bool> callback);
  void SubscribeAsync<T>(Func<T?, string, IDictionary<string, object>, Task<bool>> callback);
  string SubscriberId { get; }
}

public interface IQueuePublisher
{
  void Publish<T>(T message, string routingKey, IDictionary<string, object>? messageAttributes, int? timeToLive = null);
  void Publish<T>(IList<T> messages, string routingKey, IDictionary<string, object>? messageAttributes, int? timeToLive = null);
}

public interface IConnectionProvider : IDisposable
{
  IConnection GetProducerConnection();
  IConnection GetConsumerConnection();
  void Close();
}