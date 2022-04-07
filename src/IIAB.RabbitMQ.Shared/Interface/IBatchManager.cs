using IIAB.RabbitMQ.Shared.Models;
using RabbitMQ.Models;
using RabbitMQ.Models.Enums;

namespace IIAB.RabbitMQ.Shared.Interface;

public interface IBatchManager
{
  Task<Batch> CreateBatch(NewBatch newBatch);
  void PublishStageMessages(string batchId, BatchStage stage);
  Task<bool> ProcessBatchAction(QueueMessage<object> message);
}