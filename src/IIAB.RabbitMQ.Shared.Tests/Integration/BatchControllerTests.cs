using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using NUnit.Framework;
using RabbitMQ.Models;
using RabbitMQ.Models.Enums;

namespace RabbitMQ.Shared.Tests.Integration;

public class BatchControllerTests : BaseIntegrationTests
{
  [SetUp]
  public void Setup()
  {
    SetupBase();
  }


  [Test]
  public async Task Create_GivenValidModel_ShouldCreateBatch()
  {
    // arrange
    Setup();
    await _createAndTestANewBatch();
  }

  [Test]
  public async Task Create_Start_And_Confirm_Batch_Complete_ShouldSuccess()
  {
    // arrange
    Setup();
    DateTime waiting = DateTime.Now.AddSeconds(30);
    var batch = await _createAndTestANewBatch();
    var batchId = batch.Id;

    // Start processing
    var response = await Client.PostAsync($"/api/batch/startProcessing/{batchId}", null);
    response.IsSuccessStatusCode.Should().BeTrue();

    Batch processingBatch;
    while (true)
    {
      await Task.Delay(1000);
      processingBatch = await _getBatch(batchId);

      if (processingBatch!.IsCompleted())
        break;

      if (DateTime.Now > waiting)
        throw new TimeoutException($"Batch processing not completed in time\n{JsonSerializer.Serialize(processingBatch)}");
    }

    processingBatch.Should().NotBeNull();
    processingBatch.Id.Should().Be(batch.Id);
    processingBatch.Name.Should().Be(batch.Name);
    processingBatch.Stages
      .All(x => x.Value == batch.ItemCount)
      .Should()
      .BeTrue();
  }


  #region Private

  private async Task<Batch> _createAndTestANewBatch()
  {
    var newBatch = new NewBatch($"NewBatch:{DateTime.Now:yyyy/MM/ddTHH:mm:ss}")
    {
      ItemCount = 10,
      Stages = BatchStage.Stage1 | BatchStage.Stage2 | BatchStage.Stage3
    };

    // action
    var response = await Client.PostAsync("/api/batch/create", new StringContent(
      JsonSerializer.Serialize(newBatch),
      Encoding.UTF8,
      "application/json"));

    // assert request
    response.IsSuccessStatusCode.Should().BeTrue();

    // assert response
    var createdBatch = await _batchFromResponse(response);

    createdBatch!.Name.Should().Be(newBatch.Name);
    createdBatch.Stages.Count.Should().Be(3);

    return createdBatch;
  }
  private async Task<Batch> _getBatch(string batchId)
  {
    var response = await Client.GetAsync($"api/batch/{batchId}");

    // assert request
    response.IsSuccessStatusCode.Should().BeTrue();

    return (await _batchFromResponse(response))!;
  }

  private static async Task<Batch?> _batchFromResponse(HttpResponseMessage response)
  {
    var json = await response.Content.ReadAsStringAsync();
    if(string.IsNullOrEmpty(json))
      return null;

    var createdBatch = JsonSerializer.Deserialize<Batch>(json);
    return createdBatch;
  }
  #endregion
}

