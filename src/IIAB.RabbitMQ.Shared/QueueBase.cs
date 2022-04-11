using RabbitMQ.Client;
using RabbitMQ.Shared.Models;

namespace RabbitMQ.Shared;

public abstract class QueueBase : IDisposable
{
  protected CancellationTokenSource _cancellationTokenSource = new();
  protected CancellationToken _cancellationToken;

  protected QueueBase(CancellationTokenSource? cancellationTokenSource = null)
  {
    if(cancellationTokenSource==null)
      _cancellationTokenSource = new CancellationTokenSource();
    _cancellationToken = _cancellationTokenSource.Token;
  }

  protected void ConfigureExchange(IModel model, RabbitClientSettings settings, IDictionary<string, object>? args = null)
  {
    args ??= new Dictionary<string, object>
    {
      {"x-dead-letter-exchange", "exch-deadletter"}
    };

    model.ExchangeDeclare(
      settings.ExchangeName,
      settings.ExchangeType, arguments: args);
  }

  protected void ConfigureQueue(IModel model, RabbitConsumerSettings settings, string queueName, IDictionary<string, object>? args = null)
  {
    args ??= new Dictionary<string, object>
    {
      {"x-dead-letter-exchange", "exch-deadletter"}
    };

    model.QueueDeclare(queueName,
      durable: true,
      exclusive: false,
      autoDelete: true,
      arguments: args);
    model.QueueBind(queueName, settings.ExchangeName, settings.RouteKey);
    model.BasicQos(0, settings.PreFetchCount, false);
  }

  #region Implementation of IDisposable

  public abstract void Dispose();

  #endregion
}