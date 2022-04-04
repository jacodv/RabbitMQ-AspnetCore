using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using IIAB.RabbitMQ.Shared.Interface;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace IIAB.RabbitMQ.Shared;

public sealed class ConnectionProvider : IConnectionProvider
{
  private readonly ILogger<ConnectionProvider>? _logger;
  private readonly ConnectionFactory _factory;
  private Lazy<IConnection> _consumerConnection;
  private Lazy<IConnection> _producerConnection;

  private bool _disposed;

  public ConnectionProvider(ILogger<ConnectionProvider>? logger, string hostName)
  {
    _logger = logger;
    _factory = new ConnectionFactory
    {
      HostName = hostName,
      DispatchConsumersAsync = true,
      AutomaticRecoveryEnabled = true,
      TopologyRecoveryEnabled = true,
      RequestedHeartbeat = TimeSpan.FromSeconds(30), //TODO From Settings
      NetworkRecoveryInterval = TimeSpan.FromSeconds(10)//TODO From Settings
    };

    _producerConnection = new Lazy<IConnection>(_createConnection("Producer"));
    _consumerConnection = new Lazy<IConnection>(_createConnection("Consumer"));
  }

  public IConnection GetProducerConnection()
  {
    return _producerConnection.Value;
  }
  public IConnection GetConsumerConnection()
  {
    return _consumerConnection.Value;
  }
  public void Close()
  {
    if (_producerConnection.Value?.IsOpen==true)
      _producerConnection.Value.Close(TimeSpan.FromSeconds(1));//TODO From Settings
    if (_consumerConnection.Value?.IsOpen==true)
      _consumerConnection.Value.Close(TimeSpan.FromSeconds(1));//TODO From Settings
  }

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