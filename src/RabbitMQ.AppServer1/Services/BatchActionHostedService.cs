using RabbitMQ.Shared;
using RabbitMQ.Shared.Interface;
using RabbitMQ.Shared.Models;
using RabbitMQ.Shared.Settings;

namespace RabbitMQ.AppServer1.Services
{
  public class BatchActionHostedService: IHostedService, IDisposable
  {
    private readonly ILogger<RabbitHostedService>? _logger;
    private readonly IConnectionsProvider? _connectionProvider;
    private readonly IBatchManager _batchManager;
    private readonly string _applicationName;
    private readonly string _tag;
    private IQueueSubscriber<QueueMessage<object>>? _queueSubscriber;

    public BatchActionHostedService(
      ILogger<RabbitHostedService>? logger, 
      IConnectionsProvider? connectionProvider,
      IBatchManager batchManager,
      string applicationName,
      string tag)
    {
      _logger = logger ?? throw new ArgumentNullException(nameof(logger));
      _connectionProvider = connectionProvider;
      _batchManager = batchManager;
      _applicationName = applicationName;
      _tag = tag;
    }

    #region Implementation of IHostedService

    public Task StartAsync(CancellationToken cancellationToken)
    {
      var settings = BatchSettings
        .ForBatchActions()
        .AsRabbitConsumerSettings(BatchRouteSettings.AllActions);

      _queueSubscriber = new QueueSubscriber<QueueMessage<object>>(
        _connectionProvider!,
        _logger!,
        settings,
        _applicationName,
        _tag);

      _queueSubscriber.SubscribeAsync(_handleMessage);

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
    private async Task<bool> _handleMessage(QueueMessage<object> message, string subscriberId, IDictionary<string,object> headers)
    {
      // IGNORE Headers for now
      
      return await _batchManager.ProcessBatchAction(message);

    }
    private string _getLogLine(string action)
    {
      return $"{action} {nameof(RabbitHostedService)}[{_queueSubscriber?.SubscriberId}]";
    }
    #endregion
  }
}
