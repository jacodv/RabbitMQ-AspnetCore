using System.Net.Http;

namespace RabbitMQ.Shared.Tests.Integration;

public class BaseIntegrationTests
{
  private RabbitAppServer _appServer;
  
  protected void SetupBase()
  {
    _appServer = new RabbitAppServer();
  }

  protected HttpClient Client => _appServer.CreateClient();
}