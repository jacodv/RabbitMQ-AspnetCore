using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;

namespace RabbitMQ.Shared.Tests.Integration;

public abstract class RabbitMqTestBase<T>
{
  private readonly ServiceProvider _serviceProvider;
  protected readonly ConnectionProvider _connectionProvider;
  protected readonly ILogger<T> _logger;

  protected RabbitMqTestBase()
  {
    _serviceProvider = _buildServices();
    _connectionProvider = new ConnectionProvider(_serviceProvider.GetService<ILogger<ConnectionProvider>>(), "localhost");
    _logger = _serviceProvider.GetService<ILogger<T>>()!;  
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