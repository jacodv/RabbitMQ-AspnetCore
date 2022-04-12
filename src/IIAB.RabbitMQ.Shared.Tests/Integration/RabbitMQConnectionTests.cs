
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
      if (_connectionProvider.IsConnected)
        _connectionProvider.Close();
    }

    [Test]
    public void Constructor_GivenValidInputShouldCreateProvider()
    {
      _connectionProvider.Should().NotBeNull();
      _logger.LogDebug($"RabbitMQ is connected [{_connectionProvider.RetryCount}] : Connected:{_connectionProvider.IsConnected} - Connecting:{_connectionProvider.IsConnecting}");
    }


    [Test]
    [Explicit]
    public async Task Constructor_GivenStoppedRabbitMQ_And_RestartedRabbitMQ_ShouldConnect()
    {
      // Action
      _connectionProvider.IsConnected.Should().BeFalse();

      // Allow the manual start of RabbitMQ
      var waiting = DateTime.Now.AddSeconds(20);
      while (!_connectionProvider.IsConnected && DateTime.Now < waiting)
      {
        await Task.Delay(100);
      }

      _connectionProvider.IsConnected.Should().BeTrue();
      _connectionProvider.RetryCount.Should().BeGreaterThan(0);
    }

    [Test]
    [Explicit]
    public async Task StartedRabbitMQService_GivenStoppedRabbitMQ_And_RestartedRabbitMQ_ShouldReconnect()
    {
      // Action
      var waiting = DateTime.Now.AddSeconds(60);
      while (!_connectionProvider.GetProducerConnection().IsOpen && DateTime.Now < waiting)
      {
        await Task.Delay(100);
      }
      _connectionProvider.GetProducerConnection().IsOpen.Should().BeTrue("Rabbit did not start in time");

      // Allow the manual stop of RabbitMQ
      waiting = DateTime.Now.AddSeconds(60);
      while (_connectionProvider.GetProducerConnection().IsOpen && DateTime.Now < waiting)
      {
        await Task.Delay(100);
      }

      _connectionProvider.GetProducerConnection().IsOpen.Should().BeFalse("Rabbit did not stop in time");

      waiting = DateTime.Now.AddSeconds(90);
      while (!_connectionProvider.GetProducerConnection().IsOpen && DateTime.Now < waiting)
      {
        await Task.Delay(100);
      }

      _connectionProvider.GetProducerConnection().IsOpen.Should().BeTrue("Rabbit did not reconnect in time");
    }

  }
}
