using System.Text.Json;

namespace RabbitMQ.Shared.Models
{
  public class RabbitPublishRequest: RabbitClientSettings
  {
    public string RouteKey { get; set; } = null!;
    public QueueMessage<object> QueueMessage { get; set; } = null!;

    #region Overrides of Object

    public string ToString(bool toJson)
    {
      return toJson ? 
        JsonSerializer.Serialize(this) : 
        ToString();
    }
    public override string ToString()
    {
      return $"{ExchangeName}/{RouteKey}/{QueueMessage?.Id}";
    }

    #endregion
  }

  public class RabbitConsumerSettings: RabbitClientSettings
  {
    public const string BATCH_EXCHANGE = "exchange-batch-processing";
    public const string MISC_ECHANGE = "misc-fan-out-exchange";
    
    public string QueueName { get; set; } = null!;
    public string RouteKey { get; set; }= null!;
    public ushort PreFetchCount { get; set; } = 5;
  }

  public class RabbitClientSettings
  {
    public string ExchangeName { get; set; } = null!;
    public string ExchangeType { get; set; } = null!;
    public int? TimeToLive { get; set; }
  }
}
