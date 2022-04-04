// See https://aka.ms/new-console-template for more information

using IIAB.RabbitMQ.Shared;
using IIAB.RabbitMQ.Shared.Models;


Console.WriteLine("Before subscriber starts");
Console.ReadKey();

var queueSubscriber= new QueueSubscriber(
  new ConnectionProvider(null,"localhost"),
  null,
  null);

Func<QueueMessage<string>, IDictionary<string, object>, bool> callback = (QueueMessage<string> message, IDictionary<string, object> args) =>
{ 
  Console.WriteLine("Received Message:" + message.Body);
  return true;
};

queueSubscriber.Subscribe(callback);

Console.WriteLine("Hello, World! - Subscriber");

Console.ReadKey();
