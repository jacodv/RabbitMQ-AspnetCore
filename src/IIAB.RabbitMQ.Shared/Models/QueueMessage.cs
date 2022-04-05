
using System.Text.Json;

namespace IIAB.RabbitMQ.Shared.Models
{
  public class QueueMessage<T>
  {
    public const string BatchMessage = nameof(BatchMessage);

    public string Id { get; set; }
    public string LinkedId { get; set; }
    public string BodyType { get; set; }
    public T Body { get; set; }

    public TNew? GetBodyAs<TNew>()
    {
      return JsonSerializer.Deserialize<TNew>(JsonSerializer.Serialize(Body));
    }
  }
}
