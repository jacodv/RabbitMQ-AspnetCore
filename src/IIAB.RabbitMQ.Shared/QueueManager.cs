using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Shared.Models;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace RabbitMQ.Shared
{
  public class QueueManager: IDisposable
  {
    public void PublishMessage<T>(string queueName, QueueMessage<T> message)
    {
      PublishMessages(queueName, new []{message});
    }
    public void PublishMessages<T>(string queueName, IEnumerable<QueueMessage<T>> messages)
    {
      var factory = new ConnectionFactory() { HostName = "localhost" };
      using(var connection = factory.CreateConnection())
      using(var channel = connection.CreateModel())
      {
        channel.ModelShutdown += Channel_ModelShutdown;
        channel.BasicRecoverOk += Channel_BasicRecoverOk;
        channel.CallbackException += Channel_CallbackException;

        DeclareSingleQueue(channel, queueName);

        foreach (var message in messages)
        {
          var jsonBody = JsonSerializer.Serialize(message);
          var body = Encoding.UTF8.GetBytes(jsonBody);

          channel.BasicPublish(exchange: "",
            routingKey: queueName,
            basicProperties: null,
            body: body);

          Console.WriteLine(" [x] Sent {0}", jsonBody);
        }
      }
    }


    public void SubScribe<T>(string queueName)
    {
      var factory = new ConnectionFactory() { HostName = "localhost" };
      using var connection = factory.CreateConnection();
      using var channel = connection.CreateModel();

      channel.ModelShutdown += Channel_ModelShutdown;
      channel.BasicRecoverOk += Channel_BasicRecoverOk;
      channel.CallbackException += Channel_CallbackException;

      DeclareSingleQueue(channel, queueName);

      var subscriber = new EventingBasicConsumer(channel);
        
      subscriber.Received += Subscriber_Received;
        
      //subscriber.ConsumerCancelled += Subscriber_ConsumerCancelled;
      //subscriber.Registered += Subscriber_Registered;
      //subscriber.Shutdown += Subscriber_Shutdown;
      //subscriber.Unregistered += Subscriber_Unregistered;

      channel.BasicConsume(queue: queueName, autoAck: true, subscriber);
      Console.WriteLine("Basic Consume Called");
      Console.ReadKey();
    }

   

    public static QueueDeclareOk DeclareSingleQueue(IModel channel, string queueName)
    {
      return  channel.QueueDeclare(
        queue: queueName,
        durable: true,
        exclusive: false,
        autoDelete: false,
        arguments: null);
    }

    #region EventHandlers
    private void Channel_CallbackException(object? sender, global::RabbitMQ.Client.Events.CallbackExceptionEventArgs e)
    {
      Console.WriteLine($"Channel Callback Exception:\n{JsonSerializer.Serialize(e)}");
    }

    private void Channel_BasicRecoverOk(object? sender, EventArgs e)
    {
      Console.WriteLine($"Channel Basic Recover OK:\n{JsonSerializer.Serialize(e)}");
    }

    private void Channel_ModelShutdown(object? sender, ShutdownEventArgs e)
    {
      Console.WriteLine($"Channel Shutdown:\n{JsonSerializer.Serialize(e)}");
    }

    private void Subscriber_ConsumerCancelled(object? sender, ConsumerEventArgs e)
    {
      Console.WriteLine($"Subscriber Cancelled:\n{JsonSerializer.Serialize(e)}");
    }

    private void Subscriber_Unregistered(object? sender, ConsumerEventArgs e)
    {
      Console.WriteLine($"Subscriber Un-Registered:\n{JsonSerializer.Serialize(e)}");
    }

    private void Subscriber_Shutdown(object? sender, ShutdownEventArgs e)
    {
      Console.WriteLine($"Subscriber Shutdown:\n{JsonSerializer.Serialize(e)}");
    }

    private void Subscriber_Registered(object? sender, ConsumerEventArgs e)
    {
      Console.WriteLine($"Subscriber Registered:\n{JsonSerializer.Serialize(e)}");
    }

    private void Subscriber_Received(object? sender, BasicDeliverEventArgs e)
    {
      
      Console.WriteLine("RECEIVE");
      try
      {
        var body = e.Body.ToArray();
        var json = JsonSerializer.Deserialize<object>(Encoding.UTF8.GetString(body));
        Console.WriteLine($"RECEIVED:\n{json}");
      }
      catch (Exception exception)
      {
        Console.WriteLine(exception);
        throw;
      }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
    }

    #endregion

    #region Private

    private static void _handleMessage<T>(QueueMessage<T> message)
    {

    }
    #endregion
  }
}