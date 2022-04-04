using IIAB.RabbitMQ.Shared;
using IIAB.RabbitMQ.Shared.Interface;
using IIAB.RabbitMQ.Shared.Models;
using Microsoft.AspNetCore.Mvc;

namespace RabbitMQ.AppServer1.Controllers;

[Route("api/[controller]")]
[ApiController]
public class RabbitController : ControllerBase
{
  private readonly ILogger<RabbitController> _logger;
  private readonly IConnectionProvider _connectionProvider;

  public RabbitController(ILogger<RabbitController> logger, IConnectionProvider connectionProvider)
  {
    _logger = logger;
    _connectionProvider = connectionProvider;
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


}