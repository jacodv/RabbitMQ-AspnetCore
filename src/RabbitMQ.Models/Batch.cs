﻿using System.Text.Json.Serialization;
using FluentValidation;
using MongoDB.Bson.Serialization.Attributes;
using RabbitMQ.Models.Enums;
using UtilityData.Data;
using UtilityData.Data.Models;

namespace RabbitMQ.Models;

[BsonCollection(nameof(Batch))]
public class Batch : Document
{
  public Batch(string name)
  {
    Name = name ?? throw new ArgumentNullException(nameof(name));
    Stages = new Dictionary<string, int>();
  }
  [BsonElement("name")]
  [JsonPropertyName("name")]
  public string Name { get; set; }
  [BsonElement("itemCount")]
  [JsonPropertyName("itemCount")]
  public int ItemCount { get; set; }
  [BsonElement("stages")]
  [JsonPropertyName("stages")]
  public Dictionary<string,int> Stages { get; set; }
  [BsonElement("stageFlags")]
  [JsonPropertyName("stageFlags")]
  public BatchStage StageFlags { get; set; }
}

public class BatchValidator : AbstractValidator<Batch>
{
  public BatchValidator()
  {
  }
}