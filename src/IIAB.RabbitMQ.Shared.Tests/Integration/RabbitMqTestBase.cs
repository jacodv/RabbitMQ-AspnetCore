using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RabbitMQ.Shared.Enums;
using RabbitMQ.Shared.Interface;
using Serilog;
using Serilog.Events;

namespace RabbitMQ.Shared.Tests.Integration;

public abstract class RabbitMqTestBase<T>
{
  private readonly ServiceProvider _serviceProvider;
  protected readonly IConnectionProvider ConsumerConnectionProvider;
  protected readonly ConnectionsProvider ConnectionsProvider;
  protected readonly ILogger<T> _logger;

  protected RabbitMqTestBase()
  {
    _serviceProvider = _buildServices();
    _logger = _serviceProvider.GetService<ILogger<T>>()!;
    ConnectionsProvider = new ConnectionsProvider(_serviceProvider.GetService<ILogger<ConnectionsProvider>>(), 
      "localhost");
    ConsumerConnectionProvider = ConnectionsProvider.GetConsumerConnectionProvider;
  }

  protected ServiceProvider ServiceProvider => _serviceProvider;

  private ServiceProvider _buildServices()
  {
    Log.Logger = new LoggerConfiguration()
      .MinimumLevel.Verbose()
      .Enrich.FromLogContext()
      .WriteTo.Console()
      .WriteTo.File(
        @"c:/temp/logs/IIAB.RabbitMQ.Shared.Tests.log",
        LogEventLevel.Debug,
        retainedFileCountLimit:5,
        rollOnFileSizeLimit:true,
        fileSizeLimitBytes:10240000
      )
      .CreateLogger();

    return new ServiceCollection()
      .AddLogging(builder =>
      {
        builder.AddSerilog();
      })
      .BuildServiceProvider();
  }
}