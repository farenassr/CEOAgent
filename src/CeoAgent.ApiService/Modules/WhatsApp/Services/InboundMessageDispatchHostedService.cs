namespace CeoAgent.ApiService.Modules.WhatsApp;

public sealed partial class InboundMessageDispatchHostedService(
    IServiceScopeFactory scopeFactory,
    ILogger<InboundMessageDispatchHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan DispatchInterval = TimeSpan.FromSeconds(5);
    private const int BatchSize = 25;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(DispatchInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            await DispatchOnceAsync(stoppingToken);

            try
            {
                await timer.WaitForNextTickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task DispatchOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<InboundMessageDispatchDispatcher>();
            await dispatcher.DispatchPendingAsync(BatchSize, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            InboundMessageDispatchHostedDispatchFailed(logger, exception);
        }
    }
}
