using RabbitMQ.Client;
using RabbitMQ.Shared.Models;

namespace RabbitMQ.AppServer1.Settings
{
  public class RabbitSettings
  {
    public const string MiscellaneousConsumer = "Miscellaneous";

    public string HostName { get; set; } = "localhost";
    public int Port { get; set; } = 5672;

    public Dictionary<string, RabbitConsumerSettings> Queues { get; set; } = new Dictionary<string, RabbitConsumerSettings>()
    {
      {
        MiscellaneousConsumer, 
        new RabbitConsumerSettings()
        {
          ExchangeName = "misc-fan-out-exchange",
          ExchangeType = ExchangeType.Fanout,
          PreFetchCount = 5,
          QueueName = "misc-fan-out-queue",
          RouteKey = "misc.*",
          TimeToLive = 30000
        }}
    };
  }


}
