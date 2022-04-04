// See https://aka.ms/new-console-template for more information

using System.ComponentModel.DataAnnotations;
using IIAB.RabbitMQ.Shared;
using IIAB.RabbitMQ.Shared.Models;
using RabbitMQ.Client;

Console.WriteLine("Hello, World! - Publisher");

Console.WriteLine("Before publisher starts");
Console.ReadKey();


using var queuePublisher = new QueuePublisher(
  //amqp://guest:guest@localhost:5672"
  new ConnectionProvider(null,"localhost"), 
  null, 
  new RabbitClientSettings()
  {
    ExchangeName = "pub-sub-topic-exchange", 
    ExchangeType = ExchangeType.Topic
  });


Console.WriteLine("Enter number of messages to create:");
var messageToCreate = int.Parse(Console.ReadLine() ?? "1");

var messages = new List<QueueMessage<string>>();
foreach (var i in Enumerable.Range(1, messageToCreate))
{
  var message = new QueueMessage<string>()
  {
    Id = Guid.NewGuid().ToString(),
    BodyType = "string",
    Body = $"Sample Message: {DateTime.Now} - {i}"
  };

  messages.Add(message);
}
queuePublisher.Publish<QueueMessage<string>>(
  messages,
  "run.Create",
  null);


Console.ReadLine();

