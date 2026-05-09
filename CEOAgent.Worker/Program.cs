using CEOAgent.Worker;
using ZLogger;

var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.Logging.ClearProviders();
builder.Logging.AddZLoggerConsole();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
