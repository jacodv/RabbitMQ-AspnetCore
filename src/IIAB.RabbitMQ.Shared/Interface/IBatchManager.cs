using RabbitMQ.Models;
using RabbitMQ.Models.Enums;
using RabbitMQ.Shared.Models;

namespace RabbitMQ.Shared.Interface;

public interface IBatchManager
{
  Task<Batch> CreateBatch(NewBatch newBatch);
  void PublishStageMessages(string batchId, BatchStage stage);
  Task<bool> ProcessBatchAction(QueueMessage<object> message);
  void StartBatchProcessing(string batchId);
  Task<Batch> Get(string batchId);
}