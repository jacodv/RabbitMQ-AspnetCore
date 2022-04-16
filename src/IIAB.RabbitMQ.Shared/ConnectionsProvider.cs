using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Shared.Enums;
using RabbitMQ.Shared.Interface;

namespace RabbitMQ.Shared;

public sealed class ConnectionsProvider : IConnectionsProvider
{
  private readonly ILogger<ConnectionsProvider>? _logger;
  private readonly CancellationToken _cancellationToken;
  private readonly IConnectionProvider _consumerConnection;
  private readonly IConnectionProvider _producerConnection;

  private bool _disposed;
  private bool _closing;

  public ConnectionsProvider(ILogger<ConnectionsProvider>? logger, string hostName, CancellationToken cancellationTokenToken = default)
  {
    _logger = logger!;
    _cancellationToken = cancellationTokenToken;

    _consumerConnection = new ConnectionProvider(
      logger,
      hostName,
      RabbitMqConnectionType.Consumer,
      cancellationTokenToken);

    _producerConnection = new ConnectionProvider(
      logger,
      hostName,
      RabbitMqConnectionType.Producer,
      cancellationTokenToken);
  }

  public IConnectionProvider GetConsumerConnectionProvider => _consumerConnection;
  public IConnection GetConsumerConnection()
  {
    return _consumerConnection.GetConnection();
  }
  public IConnectionProvider GetProducerConnectionProvider => _producerConnection;
  public IConnection GetProducerConnection()
  {
    return _producerConnection.GetConnection();
  }

  public void Close()
  {
    _closing = true;
    _producerConnection.Close();
    _consumerConnection.Close();
  }

  public bool IsConsumerConnected => _consumerConnection.IsConnected;
  public bool IsProducerConnected => _producerConnection.IsConnected;

  #region Disposing pattern

  public void Dispose()
  {
    Dispose(true);
    GC.SuppressFinalize(this);
  }
  private void Dispose(bool disposing)
  {
    if (_disposed)
      return;

    if (disposing)
      Close();

    _disposed = true;
  }

  ~ConnectionsProvider()
  {
    Dispose(false);
  }

  #endregion
}