using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;

namespace RabbitMQ.Shared.Tests.Integration;

public class HealthControllerTests: BaseIntegrationTests
{

  [SetUp]
  public void Setup()
  {
    SetupBase();
  }

  [Test]
  public async Task Echo_GivenRequest_ShouldSucceed()
  {
    // arrange
    HttpRequestMessage echoRequest = new HttpRequestMessage(HttpMethod.Get, "/api/health/echo");

    // action
    var response = await Client.SendAsync(echoRequest);

    // assert
    response.IsSuccessStatusCode.Should().BeTrue();
  }
}