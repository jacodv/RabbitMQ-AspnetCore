using Microsoft.AspNetCore.Mvc;

namespace RabbitMQ.AppServer1.Controllers
{
  [Route("api/[controller]")]
  [ApiController]
  public class HealthController : ControllerBase
  {
    [HttpGet]
    [Route("echo")]
    public IActionResult Echo()
    {
      return Ok();
    }
  }
}
