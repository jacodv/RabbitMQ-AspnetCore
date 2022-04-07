using RabbitMQ.Models.Enums;

namespace RabbitMQ.Models;

public class NewBatch
{
  public NewBatch(string name)
  {
    Name = name ?? throw new ArgumentNullException(nameof(name));
  }

  public string Name { get; set; }
  public int ItemCount { get; set; }
  public BatchStage Stages { get; set; }
}