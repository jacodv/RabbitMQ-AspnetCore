using RabbitMQ.Models.Enums;

namespace RabbitMQ.Shared.Interface;

public interface IBatchMessageSender
{
  void SendBatchItemMessages<T>(IList<T> messages, string batchId, BatchStage stage);
  void SendBatchActionMessage(string batchId, string action, string? status=null);
}