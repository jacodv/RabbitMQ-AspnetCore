using System.Diagnostics;
using IIAB.RabbitMQ.Shared;
using IIAB.RabbitMQ.Shared.Interface;
using IIAB.RabbitMQ.Shared.Models;
using RabbitMQ.AppServer1.Services;
using RabbitMQ.AppServer1.Settings;
using Serilog;

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


  var rabbitSettings = new RabbitSettings();
  builder.Configuration.GetSection(nameof(RabbitSettings)).Bind(rabbitSettings);

  // Add services to the container.
  builder.Services.AddSingleton<IConnectionProvider>(x => new ConnectionProvider(x.GetService<ILogger<ConnectionProvider>>(), rabbitSettings.HostName));

  // First Service
  builder.Services.AddSingleton<IHostedService, RabbitHostedService>(x => 
    new RabbitHostedService(
      x.GetService<ILogger<RabbitHostedService>>(),
      x.GetService<IConnectionProvider>(),
      rabbitSettings.Queues[RabbitSettings.MiscellaneousConsumer]));
  
  // Second Service
  builder.Services.AddSingleton<IHostedService, RabbitHostedService>(x => 
    new RabbitHostedService(
      x.GetService<ILogger<RabbitHostedService>>(),
      x.GetService<IConnectionProvider>(),
      rabbitSettings.Queues[RabbitSettings.MiscellaneousConsumer]));

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


