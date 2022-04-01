// See https://aka.ms/new-console-template for more information

using IIAB.RabbitMQ.Shared;
using IIAB.RabbitMQ.Shared.Models;
using RabbitMQ.Client;


var queueSubscriber= new QueueSubscriber(
  new ConnectionProvider("localhost"),
  "pub-sub-topic-exchange",
  "pub-sub-queue",
  "run.*",
  ExchangeType.Topic);

Func<QueueMessage<string>, IDictionary<string, object>, bool> callback = (QueueMessage<string> message, IDictionary<string, object> args) =>
{ 
  Console.WriteLine("Received Message:" + message.Body);
  return true;
};

queueSubscriber.Subscribe(callback);

Console.WriteLine("Hello, World! - Subscriber");

Console.ReadKey();
