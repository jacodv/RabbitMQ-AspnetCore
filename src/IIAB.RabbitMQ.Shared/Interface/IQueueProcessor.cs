using IIAB.RabbitMQ.Shared.Models;

namespace IIAB.RabbitMQ.Shared.Interface;

public interface IQueueProcessor
{
  Task ProcessMessage(QueueMessage<object> message, string serviceId);
}