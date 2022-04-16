using RabbitMQ.Client;

namespace RabbitMQ.Shared.Interface;

public interface IQueueSubscriber: IDisposable
{
  void Subscribe<T>(Func<T, string, IDictionary<string, object>, bool> callback);
  void SubscribeAsync<T>(Func<T?, string, IDictionary<string, object>, Task<bool>> callback);
  string SubscriberId { get; }
  void Cancel(bool close);
}

public interface IQueuePublisher
{
  void Publish<T>(T message, string routingKey, IDictionary<string, object>? messageAttributes, int? timeToLive = null);
  void Publish<T>(IList<T> messages, string routingKey, IDictionary<string, object>? messageAttributes, int? timeToLive = null);
}

public interface IConnectionsProvider : IDisposable
{
  IConnection GetProducerConnection();
  IConnection GetConsumerConnection();
  void Close();

  bool IsConsumerConnected { get; }
  bool IsProducerConnected { get; }
  IConnectionProvider GetConsumerConnectionProvider { get; }
  IConnectionProvider GetProducerConnectionProvider { get; }
}

public interface IConnectionProvider : IDisposable
{
  IConnection GetConnection();
  void Close();

  int RetryCount { get; }
  bool IsConnecting { get;}
  bool IsConnected { get; }
}