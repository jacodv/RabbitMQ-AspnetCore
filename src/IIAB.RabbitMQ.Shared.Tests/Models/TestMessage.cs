using HotChocolate.Types;

namespace RabbitMQ.Shared.Tests.Models
{
  public sealed record TestMessage(string Body, int Number, int ProcessingMilliseconds);
}
