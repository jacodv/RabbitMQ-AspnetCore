using Microsoft.AspNetCore.Mvc;
using RabbitMQ.Models;
using RabbitMQ.Shared.Interface;

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

    [HttpPost]
    [Route("startProcessing/{batchId}")]
    public IActionResult StartProcessing(string batchId)
    {
      _batchManager.StartBatchProcessing(batchId);
      return Ok();
    }

    [HttpGet]
    [Route("{batchId}")]
    public async Task<Batch> Get(string batchId)
    {
      var batch = await _batchManager.Get(batchId);
      return batch;
    }
  }
}
