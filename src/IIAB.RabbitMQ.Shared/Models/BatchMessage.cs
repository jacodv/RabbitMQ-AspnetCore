using RabbitMQ.Models;

namespace RabbitMQ.Shared.Models
{
  public class BatchMessage
  {
    public bool IsFirst { get; set; }
    public bool IsLast { get; set; }
    public int ItemStage { get; set; } = 1;
    public int ExpectedCount { get; set; }
    public Reference? Reference { get; set; }
  }
}
