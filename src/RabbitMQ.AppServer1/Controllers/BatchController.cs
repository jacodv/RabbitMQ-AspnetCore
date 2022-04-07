using IIAB.RabbitMQ.Shared.Interface;
using Microsoft.AspNetCore.Mvc;
using RabbitMQ.Models;

namespace RabbitMQ.AppServer1.Controllers
{
  [Route("api/[controller]")]
  [ApiController]
  public class BatchController : ControllerBase
  {
    private readonly ILogger<BatchController> _logger;
    private readonly IConnectionProvider _connectionProvider;
    private readonly IBatchManager _batchManager;

    public BatchController(
      ILogger<BatchController> logger,
      IConnectionProvider connectionProvider,
      IBatchManager batchManager)
    {
      _logger = logger;
      _connectionProvider = connectionProvider;
      _batchManager = batchManager;
    }

    [HttpPost]
    [Route("create")]
    public async Task<IActionResult> Create([FromBody] NewBatch model)
    {
      var batch = await _batchManager.CreateBatch(model);
      return new JsonResult(batch);
    }
  }
}
