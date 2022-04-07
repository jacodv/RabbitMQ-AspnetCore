namespace RabbitMQ.Models;

public class Reference
{
  public Reference(string id, string name)
  {
    Id = id ?? throw new ArgumentNullException(nameof(id));
    Name = name ?? throw new ArgumentNullException(nameof(name));
  }

  public string Id { get; set; }
  public string Name { get; set; }
}