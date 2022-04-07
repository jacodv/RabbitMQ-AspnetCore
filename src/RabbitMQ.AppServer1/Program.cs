using System.Diagnostics;
using FluentValidation;
using IIAB.RabbitMQ.Shared;
using IIAB.RabbitMQ.Shared.Interface;
using IIAB.RabbitMQ.Shared.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RabbitMQ.AppServer1.Services;
using RabbitMQ.AppServer1.Settings;
using RabbitMQ.AppServer1.StartUp;
using RabbitMQ.Models;
using Serilog;
using UtilityData.Data;
using UtilityData.Data.Interfaces;
using UtilityData.Data.Settings;

Log.Logger = new LoggerConfiguration()
  .MinimumLevel.Verbose()
  .Enrich.FromLogContext()
  .WriteTo.Console()
  .CreateLogger();

try
{

  var builder = WebApplication.CreateBuilder(args);
  builder.Host.UseSerilog((hostingContext, loggerConfiguration) =>
  {
    loggerConfiguration
      .ReadFrom.Configuration(hostingContext.Configuration)
      .Enrich.FromLogContext()
      .Enrich.WithProperty("ApplicationName", typeof(Program).Assembly.GetName().Name)
      .Enrich.WithProperty("Environment", hostingContext.HostingEnvironment);

#if DEBUG
      // Used to filter out potentially bad data due debugging.
      // Very useful when doing Seq dashboards and want to remove logs under debugging session.
      loggerConfiguration.Enrich.WithProperty("DebuggerAttached", Debugger.IsAttached);
#endif
    });

  Log.Information("Starting up");

  // Mongo
  builder.Services.Configure<DatabaseSettings>(builder.Configuration.GetSection(nameof(DatabaseSettings)));
  IDatabaseSettings databaseSettings = new DatabaseSettings();
  builder.Configuration.GetSection(nameof(databaseSettings)).Bind(databaseSettings);
  builder.Services.AddSingleton(typeof(IRepository<>), typeof(MongoRepository<>));
  builder.Services.AddSingleton(serviceProvider =>
  {
    databaseSettings = serviceProvider.GetRequiredService<IOptions<DatabaseSettings>>().Value;
    return databaseSettings;
  });
  builder.Services.AddSingleton<IValidator<Batch>, BatchValidator>();
  builder.Services.AddSingleton<IValidator<BatchItem>, BatchItemValidator>();

  // Rabbit
  var rabbitSettings = new RabbitSettings();
  builder.Configuration.GetSection(nameof(RabbitSettings)).Bind(rabbitSettings);
  // Add services to the container.
  builder.Services.AddSingleton<IConnectionProvider>(x => new ConnectionProvider(x.GetService<ILogger<ConnectionProvider>>(), rabbitSettings.HostName));

  //// First Service
  //builder.Services.AddSingleton<IHostedService, RabbitHostedService>(x => 
  //  new RabbitHostedService(
  //    x.GetService<ILogger<RabbitHostedService>>(),
  //    x.GetService<IConnectionProvider>(),
  //    rabbitSettings.Queues[RabbitSettings.MiscellaneousConsumer],
  //    "WebServer",
  //    "001"));

  //// Second Service
  //builder.Services.AddSingleton<IHostedService, RabbitHostedService>(x =>
  //  new RabbitHostedService(
  //    x.GetService<ILogger<RabbitHostedService>>(),
  //    x.GetService<IConnectionProvider>(),
  //    rabbitSettings.Queues[RabbitSettings.MiscellaneousConsumer],
  //    "WebServer",
  //    "002"));

  // Add a batch manager
  builder.Services.AddSingleton<IBatchManager>(x => new BatchManager(
    x.GetService<IConnectionProvider>()!,
    x.GetService<ILogger<BatchManager>>()!,
    x.GetService<IRepository<Batch>>()!,
    x.GetService<IRepository<BatchItem>>()!,
    "AppServer001",
    "001"));

  // Add the BatchActions Processor
  builder.Services.AddSingleton<IHostedService, BatchActionHostedService>(x =>
    new BatchActionHostedService(
      x.GetService<ILogger<RabbitHostedService>>()!,
      x.GetService<IConnectionProvider>()!,
      x.GetService<IBatchManager>()!,
      "WebServer",
      "BatchActions001"));

  builder.Services.AddControllers();
  // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
  builder.Services.AddEndpointsApiExplorer();
  builder.Services.AddSwaggerGen();

  var app = builder.Build();

  // Configure the HTTP request pipeline.
  if (app.Environment.IsDevelopment())
  {
    app.UseSwagger();
    app.UseSwaggerUI();
  }

  // SetupDB
  SetupDatabase.Init(app.Services.GetService<IServiceProvider>()!).Wait();

  app.UseHttpsRedirection();

  app.UseAuthorization();

  app.MapControllers();

  app.Run();
}
catch (Exception ex)
{
  Log.Fatal(ex, "Application start-up failed");
}
finally
{
  Log.CloseAndFlush();
}


