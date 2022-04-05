using IIAB.RabbitMQ.Shared;
using IIAB.RabbitMQ.Shared.Interface;
using IIAB.RabbitMQ.Shared.Models;
using Microsoft.AspNetCore.Mvc;
using RabbitMQ.AppServer1.Services;

namespace RabbitMQ.AppServer1.Controllers;

[Route("api/[controller]")]
[ApiController]
public class RabbitController : ControllerBase
{
  private readonly ILogger<RabbitController> _logger;
  private readonly IConnectionProvider _connectionProvider;
  private readonly IEnumerable<RabbitHostedService?> _consumers;

  public RabbitController(
    ILogger<RabbitController> logger, 
    IConnectionProvider connectionProvider,
    IServiceProvider serviceProvider)
  {
    _logger = logger;
    _connectionProvider = connectionProvider;
    _consumers = serviceProvider.GetServices<IHostedService>()
      .Where(x => x is RabbitHostedService)
      .Select(x => x is RabbitHostedService instance ?
        instance:
        null);
  }

  // GET: api/<RabbitController>
  [HttpGet]
  public IEnumerable<string> Get()
  {
    return new string[] { "value1", "value2" };
  }

  // GET api/<RabbitController>/5
  [HttpGet("{id}")]
  public string Get(int id)
  {
    return "value";
  }

  // POST api/<RabbitController>
  [HttpPost]
  public void Post([FromBody] RabbitPublishRequest model)
  {
    try
    {
      using var publisher = new QueuePublisher(_connectionProvider, _logger, model);
      publisher.Publish(model.QueueMessage, model.RouteKey, null);
      _logger.LogDebug($"Published queue message: {model}");
    }
    catch (Exception e)
    {
      _logger.LogError($"Failed to publish queue message: {model.ToString(true)}", e);
      throw;
    }
  }


  [HttpPost]
  [Route("StopConsumer/{serviceNumber}")]
  public async Task StopConsumer(int serviceNumber)
  {
    try
    {
      if (_consumers == null || !_consumers.Any())
        throw new InvalidOperationException("No consumers to loaded");
      if (_consumers.Count() < serviceNumber)
        throw new IndexOutOfRangeException();

      await _consumers.Skip(serviceNumber - 1).Take(1).First().StopAsync(CancellationToken.None);

      _logger.LogDebug($"Stopping consumer: {serviceNumber}");
    }
    catch (Exception e)
    {
      _logger.LogError($"Failed to Stop consumer: {serviceNumber}", e);
      throw;
    }
  }


}