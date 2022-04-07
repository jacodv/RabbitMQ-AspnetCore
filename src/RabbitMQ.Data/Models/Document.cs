using System;
using MongoDB.Bson;
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
    public string Id { get; set; }
    public DateTime CreatedAt { get; private set; }
    public string CreatedBy { get; set; }

    public void SetCreatedAt(DateTime createdAt)
    {
      CreatedAt = createdAt;
    }
  }
}
