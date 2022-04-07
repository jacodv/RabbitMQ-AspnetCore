using System.Text.Json.Serialization;
using FluentValidation;
using MongoDB.Bson.Serialization.Attributes;
using UtilityData.Data;
using UtilityData.Data.Models;

namespace RabbitMQ.Models;

[BsonCollection(nameof(BatchItem))]
public class BatchItem : Document
{
  public BatchItem(string name, int itemNo)
  {
    Name = name;
    ItemNo = itemNo;
  }

  [BsonElement("batch")]
  [JsonPropertyName("batch")]
  public Reference Batch { get; set; }

  [BsonElement("name")]
  [JsonPropertyName("name")]
  public string Name { get; set; }

  [BsonElement("itemNo")]
  [JsonPropertyName("itemNo")]
  public int ItemNo { get; set; }

  [BsonElement("processed")]
  [JsonPropertyName("processed")]
  public bool Processed { get; set; }
}

public class BatchItemValidator : AbstractValidator<BatchItem>
{
  public BatchItemValidator()
  {
  }
}