using CeoAgent.AppHost.Configuration;

var builder = DistributedApplication.CreateBuilder(args);
var options = builder.Configuration.GetRequiredAppHostOptions();

builder.AddCeoAgentApplication(options);

await builder.Build().RunAsync();
