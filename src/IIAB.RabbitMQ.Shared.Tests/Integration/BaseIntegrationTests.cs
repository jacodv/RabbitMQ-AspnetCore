using System.Net.Http;

namespace IIAB.RabbitMQ.Shared.Tests.Integration;

public class BaseIntegrationTests
{
  private RabbitAppServer _appServer;
  
  protected void SetupBase()
  {
    _appServer = new RabbitAppServer();
  }

  protected HttpClient Client => _appServer.CreateClient();
}