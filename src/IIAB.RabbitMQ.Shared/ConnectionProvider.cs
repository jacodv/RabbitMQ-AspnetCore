using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using HotChocolate.Language;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Shared.Enums;
using RabbitMQ.Shared.Interface;

namespace RabbitMQ.Shared;

public sealed class ConnectionProvider : IConnectionProvider
{
  private readonly ILogger<ConnectionsProvider>? _logger;
  private readonly RabbitMqConnectionType _connectionType;
  private readonly CancellationToken _cancellationToken;
  private Lazy<IConnection>? _connection;
  private Task _connectingTask;
  private Task _reconnectingTask;
  private object _locker = new();
  private readonly int _connectionProviderTimeoutSeconds;

  private bool _disposed;
  private bool _closing;

  public ConnectionProvider(
    ILogger<ConnectionsProvider>? logger,
    string hostName,
    RabbitMqConnectionType connectionType,
    CancellationToken cancellationTokenToken = default)
  {
    _logger = logger!;
    _connectionType = connectionType;
    _cancellationToken = cancellationTokenToken;
    _connectionProviderTimeoutSeconds = 60; //TODO From Settings
    var factory = new ConnectionFactory
    {
      HostName = hostName,
      DispatchConsumersAsync = true,
      AutomaticRecoveryEnabled = true,
      TopologyRecoveryEnabled = true,
      RequestedHeartbeat = TimeSpan.FromSeconds(30),//<-- Defaults --> //TODO From Settings
      NetworkRecoveryInterval = TimeSpan.FromSeconds(5)//<-- Defaults --> //TODO From Settings
    };

    IsConnecting = true;
    _connectingTask = Task.Factory.StartNew<Task>(async () =>
    {
      var sw = new Stopwatch();
      sw.Start();
      while (IsConnecting &&
             !_cancellationToken.IsCancellationRequested &&
             RetryCount <= _connectionProviderTimeoutSeconds)
      {
        try
        {
          _connection = new Lazy<IConnection>(_createConnection(factory));

          IsConnecting = false;
          break;
        }
        catch (Exception ex)
        {
          _logger.LogWarning($"Connection [{RetryCount}]({GetConnectionName(_connectionType)}) to RabbitMQ could not be established - [{ex.Message}]");
        }

        RetryCount++;
        await Task.Delay(1000);
      }

      IsConnecting = false;
      sw.Stop();
      if (!IsConnected)
        _logger.LogError($"RabbitMQ connection permanently timed out: Retries: {RetryCount} - {sw.Elapsed}");
    }, _cancellationToken);
  }

  public IConnection GetConnection()
  {
    return _returnConnection();
  }
  public void Close()
  {
    _closing = true;

    if (_connection?.Value?.IsOpen == true)
      _connection.Value.Close(TimeSpan.FromSeconds(1));//TODO From Settings
  }

  public int RetryCount { get; private set; }
  public bool IsConnecting { get; private set; }
  public bool IsConnected
  {
    get
    {
      if (IsConnecting)
        return false;

      return _connection?.Value.IsOpen ?? false;
    }
  }

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
  private IConnection _createConnection(IConnectionFactory factory)
  {
    factory.ClientProvidedName = $"{GetConnectionName(_connectionType)}";
    var connection = factory.CreateConnection();

    _logger?.LogDebug($"Creating event listeners for: {GetConnectionName(_connectionType)}");
    connection.ConnectionShutdown += _connectionShutdown;
    connection.CallbackException += _connectionCallbackException;
    connection.ConnectionBlocked += _connectionBlocked;
    connection.ConnectionUnblocked += _connectionUnblocked;

    _logger?.LogInformation($"Created RabbitMQ connection: {GetConnectionName(_connectionType)}");

    return connection;
  }
  private void _connectionUnblocked(object sender, EventArgs e)
  {
    _logger?.LogInformation($"RabbitMQ connection un-blocked: {GetConnectionName(_connectionType)}");
  }
  private void _connectionBlocked(object sender, EventArgs e)
  {
    _logger?.LogWarning($"RabbitMQ connection blocked: {GetConnectionName(_connectionType)}");
  }
  private void _connectionCallbackException(object sender, CallbackExceptionEventArgs args)
  {
    _logger?.LogError($"RabbitMQ connection callback exception: {GetConnectionName(_connectionType)}\n{JsonSerializer.Serialize(args.Detail)}", args.Exception);
  }
  private void _connectionShutdown(object sender, ShutdownEventArgs args)
  {
    _logger?.LogInformation($"RabbitMQ connection shutdown: {GetConnectionName(_connectionType)} - {args.ReplyText}");
    if (_closing) return;

    lock (_locker)
    {
      if (IsConnecting)
        return;
      IsConnecting = true;
      _logger?.LogDebug($"RabbitMQ starting reconnection task: {GetConnectionName(_connectionType)}");
    }

#pragma warning disable CS4014
    _waitForReconnecting();
#pragma warning restore CS4014

    _logger?.LogDebug($"RabbitMQ started reconnection task: {GetConnectionName(_connectionType)}");
  }
  private IConnection _returnConnection()
  {
    if (!IsConnected && !IsConnecting)
      throw new InvalidOperationException("No RabbitMQ connection available");

    _waitForOrTimeOut().Wait(_cancellationToken);

    return _connection!.Value;
  }
  private async Task _waitForOrTimeOut()
  {
    var waitUntil = DateTime.Now.AddSeconds(_connectionProviderTimeoutSeconds);
    while (IsConnecting && DateTime.Now < waitUntil)
    {
      _logger?.LogDebug($"Waiting for connection: IsConnecting:{IsConnecting} ({GetConnectionName(_connectionType)}): {DateTime.Now} < {waitUntil} = {DateTime.Now < waitUntil}");
      await Task.Yield();
      await Task.Delay(1000, _cancellationToken);
    }
  }
  private async Task _waitForReconnecting()
  {
      var waitUntil = DateTime.Now.AddSeconds(_connectionProviderTimeoutSeconds);
      while (IsConnecting && DateTime.Now < waitUntil)
      {
        _logger?.LogDebug($"RabbitMQ waiting for reconnection: {DateTime.Now} < {waitUntil} = {DateTime.Now < waitUntil}");

        var connection = _connection?.Value;
        if (_cancellationToken.IsCancellationRequested || connection?.IsOpen == true)
        {
          IsConnecting = false;
          break;
        }
        await Task.Delay(1000, _cancellationToken);
        await Task.Yield();
      }
  }
  #endregion

  private static string GetConnectionName(RabbitMqConnectionType type)
  {
    return $"{GetIpAddress()}-{type}";
  }
  private static string GetIpAddress()
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