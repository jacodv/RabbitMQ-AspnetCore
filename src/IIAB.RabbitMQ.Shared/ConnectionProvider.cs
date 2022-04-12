using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Shared.Interface;

namespace RabbitMQ.Shared;

public sealed class ConnectionProvider : IConnectionProvider
{
  private const string ConsumerConnectionName = "Consumer";
  private const string ProducerConnectionName = "Producer";

  private readonly ILogger<ConnectionProvider>? _logger;
  private readonly CancellationToken _cancellationToken;
  private readonly ConnectionFactory _factory;
  private Lazy<IConnection> _consumerConnection;
  private Lazy<IConnection> _producerConnection;
  private Task _connectingTask;

  private bool _disposed;

  public ConnectionProvider(ILogger<ConnectionProvider>? logger, string hostName, CancellationToken cancellationTokenToken = default)
  {
    _logger = logger!;
    _cancellationToken = cancellationTokenToken;
    _factory = new ConnectionFactory
    {
      HostName = hostName,
      DispatchConsumersAsync = true,
      AutomaticRecoveryEnabled = true,
      TopologyRecoveryEnabled = true,
      RequestedHeartbeat = TimeSpan.FromSeconds(10), //TODO From Settings
      NetworkRecoveryInterval = TimeSpan.FromSeconds(10)//TODO From Settings
    };

    IsConnecting = true;
    _connectingTask = Task.Factory.StartNew(() =>
    {
      var sw = new Stopwatch();
      sw.Start();
      while (IsConnecting && !_cancellationToken.IsCancellationRequested && RetryCount <= 30)
      {
        try
        {
          _producerConnection = new Lazy<IConnection>(_createConnection(ProducerConnectionName));
          _consumerConnection = new Lazy<IConnection>(_createConnection(ConsumerConnectionName));

          IsConnecting = false;
          IsConnected = true;
          break;
        }
        catch(Exception ex)
        {
          _logger.LogWarning($"Connection to RabbitMQ could not be established - [{ex.Message}]");
        }

        RetryCount++;
        Thread.Sleep(1000);
      }

      IsConnecting = false;
      sw.Stop();
      _logger.LogError($"RabbitMQ connection permanently timed out: Retries: {RetryCount} - {sw.Elapsed}");
    }, _cancellationToken);
  }

  public IConnection GetProducerConnection()
  {
    return _returnConnection(ProducerConnectionName);
  }

  public IConnection GetConsumerConnection()
  {
    return _returnConnection(ConsumerConnectionName);
  }
  public void Close()
  {
    if (_producerConnection.Value?.IsOpen==true)
      _producerConnection.Value.Close(TimeSpan.FromSeconds(1));//TODO From Settings
    if (_consumerConnection.Value?.IsOpen==true)
      _consumerConnection.Value.Close(TimeSpan.FromSeconds(1));//TODO From Settings
  }

  public int RetryCount { get; private set; }
  public bool IsConnected { get; private set; }
  public bool IsConnecting { get; private set; }

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

  ~ConnectionProvider()
  {
    Dispose(false);
  }

  #endregion

  #region Private
  private IConnection _createConnection(string type)
  {
    _factory.ClientProvidedName = $"{GetIpAddress()}-{type}";
    var connection = _factory.CreateConnection();
    connection.ConnectionShutdown += (o,e) => _connectionOnConnectionShutdown(type, e);
    connection.CallbackException += ((sender, args) => _connectionCallbackException(type, args));
    connection.ConnectionBlocked += (sender, args) => _connectionBlocked(type);
    connection.ConnectionUnblocked += (sender, args) => _connectionUnblocked(type);

    _logger?.LogInformation($"Created RabbitMQ connection: {GetConnectionName(type)}");

    return connection;
  }
  private void _connectionUnblocked(string type)
  {
    _logger?.LogInformation($"RabbitMQ connection un-blocked: {GetConnectionName(type)}");
  }
  private void _connectionBlocked(string type)
  {
    _logger?.LogWarning($"RabbitMQ connection blocked: {GetConnectionName(type)}");
  }
  private void _connectionCallbackException(string type, CallbackExceptionEventArgs args)
  {
    _logger?.LogError($"RabbitMQ connection callback exception: {GetConnectionName(type)}\n{JsonSerializer.Serialize(args.Detail)}",args.Exception);
  }
  private void _connectionOnConnectionShutdown(string type, ShutdownEventArgs args)
  {
    _logger?.LogInformation($"RabbitMQ connection shutdown: {GetConnectionName(type)} - {args.ReplyText}");
  }
  private IConnection _returnConnection(string type)
  {
    while (IsConnecting)
    {
      Thread.Sleep(100);
    }
    if (!IsConnected && !IsConnecting)
      throw new InvalidOperationException("No RabbitMQ connection available");

    return type==ProducerConnectionName?
        _producerConnection.Value:
        _consumerConnection.Value;
  }
  #endregion

  public static string GetConnectionName(string type)
  {
    return $"{GetIpAddress()}-{type}";
  }
  public static string GetIpAddress()
  {
    var ipHostInfo = Dns.GetHostEntry(Dns.GetHostName()); // `Dns.Resolve()` method is deprecated.
    foreach (var address in ipHostInfo.AddressList)
    {
      if (address.AddressFamily == AddressFamily.InterNetwork)
        return address.ToString();
    }
      
    throw new InvalidOperationException("Ipv4 not found");
  }
}