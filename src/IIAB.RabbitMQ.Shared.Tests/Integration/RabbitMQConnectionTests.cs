
using System;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace RabbitMQ.Shared.Tests.Integration
{
  public class RabbitMqConnectionTests : RabbitMqTestBase<RabbitMqConnectionTests>
  {
    [SetUp]
    public void Setup()
    {
    }

    [TearDown]
    public void TearDown()
    {
      ConnectionsProvider?.Close();
    }

    [Test]
    [Order(1)]
    public void Constructor_GivenValidInputShouldCreateProvider()
    {
      ConsumerConnectionProvider.Should().NotBeNull();
    }

    [Test]
    [Explicit]
    public async Task CreatedConnectionProvider_GivenValidInputShouldCreateProvider()
    {
      await Task.Delay(100); // Give the connection time to start
      ConsumerConnectionProvider.Should().NotBeNull();
      ConsumerConnectionProvider.IsConnected.Should().BeTrue();
    }

    [Test]
    [Explicit]
    public async Task Constructor_GivenStoppedRabbitMQ_And_RestartedRabbitMQ_ShouldConnect()
    {
      // Action
      ConsumerConnectionProvider.IsConnected.Should().BeFalse();

      // Allow the manual start of RabbitMQ
      var waiting = DateTime.Now.AddSeconds(60);
      while (!ConsumerConnectionProvider.IsConnected && DateTime.Now < waiting)
      {
        await Task.Delay(100);
      }

      ConsumerConnectionProvider.IsConnected.Should().BeTrue();
    }

    [Test]
    [Explicit]
    public async Task StartedRabbitMQService_GivenStoppedRabbitMQ_And_RestartedRabbitMQ_ShouldReconnect()
    {
      // Action
      var waiting = DateTime.Now.AddSeconds(60);
      _logger.LogDebug($"TEST: Waiting for Rabbit Services Connection");
      while (!ConsumerConnectionProvider.IsConnected && DateTime.Now < waiting)
      {
        await Task.Delay(100);
      }
      ConsumerConnectionProvider.IsConnected.Should().BeTrue("Rabbit did not start in time");

      // Allow the manual stop of RabbitMQ
      waiting = DateTime.Now.AddSeconds(60);
      _logger.LogDebug($"TEST: Waiting for Rabbit Services to Stop");
      while (ConsumerConnectionProvider.IsConnected && DateTime.Now < waiting)
      {
        await Task.Yield();
      }

      ConsumerConnectionProvider.IsConnected.Should().BeFalse("Rabbit did not stop in time");

      waiting = DateTime.Now.AddSeconds(90);
      _logger.LogDebug($"TEST: Waiting for Rabbit Services Reconnect");
      while (!ConsumerConnectionProvider.IsConnected && DateTime.Now < waiting)
      {
        await Task.Delay(100);
      }

      ConsumerConnectionProvider.IsConnected.Should().BeTrue("Rabbit did not reconnect in time");
    }

  }
}
