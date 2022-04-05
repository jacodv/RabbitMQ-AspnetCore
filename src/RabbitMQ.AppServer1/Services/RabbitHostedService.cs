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
    private readonly MiscellaneousQueueProcessor _miscProcessor;

    public RabbitHostedService(
      ILogger<RabbitHostedService>? logger, 
      IConnectionProvider? connectionProvider,
      RabbitConsumerSettings consumerSettings)
    {
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
      _connectionProvider = connectionProvider;
      _consumerSettings = consumerSettings;
      _miscProcessor = new MiscellaneousQueueProcessor(logger, _connectionProvider!);
    }

    #region Overrides of BackgroundService

    public Task StartAsync(CancellationToken cancellationToken)
    {
      _queueSubscriber = new QueueSubscriber(
        _connectionProvider,
        _logger,
        _consumerSettings);

      _queueSubscriber.SubscribeAsync<QueueMessage<object>>(_handleMessage);

      _logger?.LogInformation(_getLogLine("Starting"));

      return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
      _logger?.LogInformation(_getLogLine("Stopping"));
      _queueSubscriber?.Dispose();
      _miscProcessor.Dispose();
      return Task.CompletedTask;
    }

    public void Dispose()
    {
      _logger?.LogInformation(_getLogLine("Disposing"));
      _queueSubscriber?.Dispose();
      _miscProcessor?.Dispose();
    }

    #endregion

    #region Private

    private async Task<bool> _handleMessage(QueueMessage<object> message, string subscriberId, IDictionary<string,object> headers)
    {
      // IGNORE Headers for now
      await _miscProcessor.ProcessMessage(message, subscriberId);
      return true;
    }

    private string _getLogLine(string action)
    {
      return $"{action} {nameof(RabbitHostedService)}[{_queueSubscriber.SubscriberId}]:\n{JsonSerializer.Serialize(_consumerSettings)}";
    }
    #endregion
  }
}
