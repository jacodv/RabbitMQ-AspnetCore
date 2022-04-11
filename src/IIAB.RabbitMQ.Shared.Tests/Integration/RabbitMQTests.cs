using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using RabbitMQ.Client;
using RabbitMQ.Shared.Models;
using Serilog;
using Serilog.Events;

namespace RabbitMQ.Shared.Tests.Integration;

public class RabbitMQTests
{
  private QueuePublisher _publisher;
  private ILogger<RabbitMQTests> _logger;
  private QueueSubscriber _subscriber;
  [SetUp]
  public void Setup()
  {
    var serviceProvider = _buildServices();

    var settings = _getRabbitMqSettings();
    var connectionProvider = new ConnectionProvider(serviceProvider.GetService<ILogger<ConnectionProvider>>(), "localhost");
    _logger = serviceProvider.GetService<ILogger<RabbitMQTests>>()!;  

    _createSubscribers(connectionProvider, settings);

    _publisher = new QueuePublisher(
      connectionProvider,
      _logger,
      settings);

  }

  private void _createSubscribers(ConnectionProvider connectionProvider, RabbitConsumerSettings settings)
  {
    _subscriber = new QueueSubscriber(
      connectionProvider,
      _logger,
      settings,
      "RabbitMQTests",
      "001");
    _subscriber.SubscribeAsync<QueueMessage<string>>();

    _subscriber = new QueueSubscriber(
      connectionProvider,
      _logger,
      settings,
      "RabbitMQTests",
      "002");


  }

  private RabbitConsumerSettings _getRabbitMqSettings()
  {
    return new RabbitConsumerSettings()
    {
      ExchangeName = "exch-topic-tests",
      ExchangeType = ExchangeType.Topic,
      PreFetchCount = 5,
      QueueName = "queue-topic-tests",
      RouteKey = "queue-topic.*",
    };
  }

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