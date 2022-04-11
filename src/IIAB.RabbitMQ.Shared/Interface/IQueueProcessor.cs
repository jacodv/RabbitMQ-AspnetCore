using RabbitMQ.Shared.Models;

namespace RabbitMQ.Shared.Interface;

public interface IQueueProcessor
{
  Task ProcessMessage(QueueMessage<object> message, string serviceId);
}