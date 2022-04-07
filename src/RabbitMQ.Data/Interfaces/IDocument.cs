using System;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace UtilityData.Data.Interfaces
{
  public interface IDocument
  {
    [BsonId]
    [BsonRepresentation(BsonType.String)]
    string Id { get; set; }

    DateTime CreatedAt { get; }
  }
}