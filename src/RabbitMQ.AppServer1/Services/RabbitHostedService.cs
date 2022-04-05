using System.Text.Json;
using IIAB.RabbitMQ.Shared;
using IIAB.RabbitMQ.Shared.Interface;
using IIAB.RabbitMQ.Shared.Models;

namespace RabbitMQ.AppServer1.Services
{
  public class RabbitHostedService : IHostedService,IDisposable
  {
    private readonly ILogger<RabbitHostedService>? _logger;
    private readonly IConnectionProvider? _connectionProvider;
    private readonly RabbitConsumerSettings _consumerSettings;
    private IQueueSubscriber _queueSubscriber;
    private string _serviceId = Guid.NewGuid().ToString();

    public RabbitHostedService(
      ILogger<RabbitHostedService>? logger, 
      IConnectionProvider? connectionProvider,
      RabbitConsumerSettings consumerSettings)
    {
      _logger = logger;
      _connectionProvider = connectionProvider;
      _consumerSettings = consumerSettings;
    }

    #region Overrides of BackgroundService

    public Task StartAsync(CancellationToken cancellationToken)
    {
      _queueSubscriber = new QueueSubscriber(
        _connectionProvider,
        _logger,
        _consumerSettings,
        ConnectionProvider.GetConnectionName(_serviceId));

      _queueSubscriber.SubscribeAsync<QueueMessage<object>>(_handleMessage);

      _logger?.LogInformation(_getLogLine("Starting"));

      return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
      _logger?.LogInformation(_getLogLine("Stopping"));
      _queueSubscriber?.Dispose();
      return Task.CompletedTask;
    }

    public void Dispose()
    {
      _logger?.LogInformation(_getLogLine("Disposing"));
      _queueSubscriber?.Dispose();
    }

    #endregion

    #region Private

    private Task<bool> _handleMessage(QueueMessage<object> message, IDictionary<string,object> headers)
    {
      _logger?.LogDebug($"Handling message [{_serviceId}] :\nHeaders:{JsonSerializer.Serialize(headers)} \nMessage:{JsonSerializer.Serialize(message)}");
      return Task.FromResult(true);
    }

    private string _getLogLine(string action)
    {
      return $"{action} {nameof(RabbitHostedService)}[{_serviceId}]:\n{JsonSerializer.Serialize(_consumerSettings)}";
    }
    #endregion
  }
}
