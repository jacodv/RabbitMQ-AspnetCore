using System;
using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using UtilityData.Data.Interfaces;

namespace UtilityData.Data.Models
{
  public abstract class Document : IDocument
  {
    protected Document()
    {
      var id = ObjectId.GenerateNewId();
      Id=id.ToString();
      CreatedAt = id.CreationTime;
    }
    [JsonPropertyName("id")]
    public string Id { get; set; }
    public DateTime CreatedAt { get; private set; }
    public string CreatedBy { get; set; }

    public void SetCreatedAt(DateTime createdAt)
    {
      CreatedAt = createdAt;
    }
  }
}
