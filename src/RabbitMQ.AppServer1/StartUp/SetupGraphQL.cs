using HotChocolate.AspNetCore.Extensions;
using HotChocolate.Types.Pagination;
using RabbitMQ.AppServer1.GraphQL;
using RabbitMQ.Models.Enums;


namespace RabbitMQ.AppServer1.StartUp;

public static class SetupGraphQL
{
  public static void AddHC(this WebApplicationBuilder builder)
  {
    builder.Services
      .AddGraphQLServer()
      .AddQueryType(d => d.Name("Query"))
        .AddTypeExtension<BatchQueries>()
        .AddTypeExtension<BatchItemQueries>()
      .AddSubscriptionType(d => d.Name("Subscription"))
        .AddTypeExtension<BatchSubscription>()
        //.AddTypeExtension<BatchItemSubscription>()
      .AddType<BatchModelType>()
      .AddFiltering()
      .AddSorting()
      .AddInMemorySubscriptions()
      .ModifyRequestOptions(
        o =>
        {
          o.IncludeExceptionDetails = true;
        })
      .SetPagingOptions(new PagingOptions()
      {
        DefaultPageSize = 100,
        MaxPageSize = 500,
        IncludeTotalCount = true
      });
  }
}


 