namespace IIAB.RabbitMQ.Shared.Models
{
  public class QueueMessage<T>
  {
    public string Id { get; set; }
    public string LinkedId { get; set; }
    public string BodyType { get; set; }
    public T Body { get; set; }
  }
}
