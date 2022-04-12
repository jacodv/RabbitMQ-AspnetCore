using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FizzWare.NBuilder;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using RabbitMQ.Client;
using RabbitMQ.Shared.Models;
using RabbitMQ.Shared.Tests.Models;

namespace RabbitMQ.Shared.Tests.Integration;

public class RabbitMQTests: RabbitMqTestBase<RabbitMQTests>
{
  private readonly ConcurrentDictionary<string,ConcurrentBag<TestMessage>> _processedMessage=new();
  
  private QueueSubscriber? _topicSubscriber1;
  private QueueSubscriber? _topicSubscriber2;
  private QueueSubscriber? _fanoutSubscriber1;
  private QueueSubscriber? _fanoutSubscriber2;

  private QueuePublisher _topicPublisher;
  private QueuePublisher _fanoutPublisher;
  
  private const string _routeKeyPattern = "test-route.{0}";
  private CancellationTokenSource _cancellationTokenSource = new ();
  private bool _cancelling;
  private bool _throwOnMessage5;

  public new void Setup(ushort? prefetchCount=null)
  {
    _createSubscribersAndPublishers(_connectionProvider, prefetchCount);
  }

  [TearDown]
  public void TearDown()
  {
    _topicPublisher?.Dispose();
    _topicSubscriber1?.Dispose();
    _topicSubscriber2?.Dispose();

    _fanoutPublisher?.Dispose();
    _fanoutSubscriber1?.Dispose();
    _fanoutSubscriber2?.Dispose();

    _connectionProvider.Close();
  }

  [Test]
  public async Task Publish_10_Messages_ShouldProcessEvenly_BySubscribers()
  {
    // Setup
    Setup();
    var messages = Builder<TestMessage>
      .CreateListOfSize(10)
      .All()
      .WithFactory((i => new TestMessage("Body", i, 10)))
      .Build();

    // Action
    _topicPublisher.Publish(messages, string.Format(_routeKeyPattern,"Processing"), null);
    await Task.Delay(1000);

    //Assert
    _processedMessage[_topicSubscriber1.SubscriberId].Count
      .Should()
      .Be(_processedMessage[_topicSubscriber2.SubscriberId].Count)
      .And
      .Be(5);
  }

  [Test]
  public async Task Publish_10_Messages_ShouldProcessAllByAllSubscribers()
  {
    // Setup
    Setup();
    var messages = Builder<TestMessage>
      .CreateListOfSize(10)
      .All()
      .WithFactory((i => new TestMessage("Body", i, 10)))
      .Build();

    // Action
    _fanoutPublisher.Publish(messages, string.Format(_routeKeyPattern,"Processing"), null);
    await Task.Delay(1000);

    //Assert
    _processedMessage[_fanoutSubscriber1!.SubscriberId].Count
      .Should()
      .Be(_processedMessage[_fanoutSubscriber2!.SubscriberId].Count)
      .And
      .Be(10);
  }

  [Test]
  public async Task KillConsumer_GivenUnProcessedMessage_ShouldRequeueMessages()
  {
    // Setup
    ushort? preFetchCount = 1;
    Setup(preFetchCount);
    var messages = Builder<TestMessage>
      .CreateListOfSize(10)
      .All()
      .WithFactory((i =>
          {
            var number = i + 1;
            return new TestMessage($"Body{number}", number, number % 2 == 0 ? 1000 : 10);
          }
        ))
      .Build();

    // Action
    _topicPublisher.Publish(messages, string.Format(_routeKeyPattern,"Processing"), null);
    await Task.Delay(500);
    _fanoutSubscriber2?.Cancel(true);
    _cancelling = true;

    //Assert
    await Task.Delay(10000);
    _logger.LogDebug($"Subscriber 1 processed: {_processedMessage[_topicSubscriber1!.SubscriberId].Count}");
    _logger.LogDebug($"Subscriber 2 processed: {_processedMessage[_topicSubscriber2!.SubscriberId].Count}");

    var options = new JsonSerializerOptions() { WriteIndented = true };

    _logger.LogDebug($"Processed messages\n{JsonSerializer.Serialize(_processedMessage, options)}");
    _processedMessage[_topicSubscriber1!.SubscriberId].Count
      .Should()
      .Be(10);
  }

  [Test]
  public async Task Publish_10_Messages_With1Exception_ShouldProcessEvenly_BySubscribers()
  {
    // Setup
    Setup();
    var messages = Builder<TestMessage>
      .CreateListOfSize(10)
      .All()
      .WithFactory((i => new TestMessage("Body", i, 10)))
      .Build();

    // Action
    _throwOnMessage5 = true;
    _topicPublisher.Publish(messages, string.Format(_routeKeyPattern,"Processing"), null);
    await Task.Delay(1000);

    //Assert
    _processedMessage[_topicSubscriber1!.SubscriberId].Count
      .Should()
      .Be(5);
    _processedMessage[_topicSubscriber2!.SubscriberId].Count
      .Should()
      .Be(4);
  }

  #region Private

  private void _createSubscribersAndPublishers(ConnectionProvider connectionProvider, ushort? prefetchCount=null)
  {
    var topicSettings = _getRabbitMqSettings(ExchangeType.Topic, "topic-tests");
    if(prefetchCount.HasValue)
      topicSettings.PreFetchCount = prefetchCount.Value;
    _topicSubscriber1 = _createSubscriber(connectionProvider, topicSettings, "TopicService", "001");
    _topicSubscriber2 = _createSubscriber(connectionProvider, topicSettings, "TopicService", "002");
    _topicPublisher = new QueuePublisher(connectionProvider, _logger, topicSettings, _cancellationTokenSource);

    var fanOutSettings = _getRabbitMqSettings(ExchangeType.Fanout, "fanout-tests");
    if(prefetchCount.HasValue)
      fanOutSettings.PreFetchCount = prefetchCount.Value;
    _fanoutSubscriber1 = _createSubscriber(connectionProvider, fanOutSettings, "FanOutService", "001");
    _fanoutSubscriber2 = _createSubscriber(connectionProvider, fanOutSettings, "FanOutService", "002");
    _fanoutPublisher = new QueuePublisher(connectionProvider, _logger, fanOutSettings, _cancellationTokenSource);
  }

  private QueueSubscriber? _createSubscriber(ConnectionProvider connectionProvider, RabbitConsumerSettings topicSettings, string appName, string tagName)
  {
    var subscriber = new QueueSubscriber(
      connectionProvider,
      _logger,
      topicSettings,
      appName,
      tagName,
      _cancellationTokenSource);
    subscriber.SubscribeAsync<TestMessage>(
      _messageHandler!);
    _processedMessage.TryAdd(subscriber.SubscriberId, new ConcurrentBag<TestMessage>());

    return subscriber; 
  }

  private async Task<bool> _messageHandler(TestMessage message, string subscriberId, IDictionary<string, object> headers)
  {
    _logger.LogDebug($"Handling[{subscriberId}] message: {message}");
    
    if (_throwOnMessage5 && message.Number == 5)
      throw new InvalidOperationException("ThrowOn5 is true");

    await Task.Delay(message!.ProcessingMilliseconds, _cancellationTokenSource.Token);
    if (_cancellationTokenSource.IsCancellationRequested || (_cancelling && subscriberId==_topicSubscriber2?.SubscriberId))
    {
      _logger.LogInformation($"Processing[{subscriberId}] cancelled for message: {message}");
      return false;
    }
    _processedMessage[subscriberId].Add(message);
    _logger.LogDebug($"{subscriberId} processed message:{message}");
    return true;
  }
  private RabbitConsumerSettings _getRabbitMqSettings(string exchangeType, string queueName)
  {
    return new RabbitConsumerSettings
    {
      ExchangeName = $"exch-{queueName}",
      ExchangeType = exchangeType,
      PreFetchCount = 5,
      QueueName = $"queue-{queueName}",
      RouteKey = string.Format(_routeKeyPattern, "*"),
    };
  }

  #endregion
}