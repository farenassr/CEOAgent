using System.Text.Json;
using Azure.Storage.Queues;
using CeoAgent.Application;
using CeoAgent.Integrations.Jobs;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using ZLogger;

namespace CeoAgent.Worker.Jobs;

/// <summary>
/// Polls the incoming message queue, dispatches jobs to the processor, and moves exhausted messages to poison storage.
/// </summary>
public sealed class IncomingMessageQueueWorker(
    QueueServiceClient queues,
    IServiceScopeFactory scopeFactory,
    IOptions<IncomingQueueOptions> options,
    WorkerHealthTracker healthTracker,
    ILogger<IncomingMessageQueueWorker> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// Runs the queue polling loop until cancellation, deleting successful messages and preserving failed poison messages.
    /// </summary>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        var queue = queues.GetQueueClient(IncomingMessageQueueNames.ProcessIncomingMessage);
        var poisonQueue = queues.GetQueueClient(IncomingMessageQueueNames.ProcessIncomingMessage + settings.PoisonQueueSuffix);
        await queue.CreateIfNotExistsAsync(cancellationToken: stoppingToken);
        await poisonQueue.CreateIfNotExistsAsync(cancellationToken: stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            healthTracker.RecordPoll();
            try
            {
                var properties = await queue.GetPropertiesAsync(cancellationToken: stoppingToken);
                CeoAgentTelemetry.SetQueueBacklog(properties.Value.ApproximateMessagesCount);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to retrieve queue properties for backlog telemetry.");
            }

            var messages = await queue.ReceiveMessagesAsync(
                maxMessages: settings.MaxMessages > 0 ? settings.MaxMessages : 1,
                visibilityTimeout: TimeSpan.FromMinutes(Math.Max(1, settings.VisibilityTimeoutMinutes)),
                cancellationToken: stoppingToken);

            if (messages.Value.Length == 0)
            {
                await Task.Delay(
                    TimeSpan.FromMilliseconds(Math.Max(100, settings.EmptyQueueDelayMilliseconds)),
                    stoppingToken);
                continue;
            }

            CeoAgentTelemetry.QueueDequeueCount.Add(messages.Value.Length);

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = settings.MaxDegreeOfParallelism > 0 ? settings.MaxDegreeOfParallelism : 1,
                CancellationToken = stoppingToken,
            };

            await Parallel.ForEachAsync(messages.Value, parallelOptions, async (message, ct) =>
            {
                await ProcessSingleAsync(queue, poisonQueue, message, ct);
            });
        }
    }

    private async Task ProcessSingleAsync(
        QueueClient queue,
        QueueClient poisonQueue,
        Azure.Storage.Queues.Models.QueueMessage message,
        CancellationToken stoppingToken)
    {
        var stopwatch = Stopwatch.StartNew();
        ProcessIncomingMessageJob? job = null;
        try
        {
            job = JsonSerializer.Deserialize<ProcessIncomingMessageJob>(
                message.MessageText,
                SerializerOptions)
                ?? throw new InvalidOperationException("Invalid incoming message job payload.");

            using var logScope = BeginJobScope(job);
            using var scope = scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<ProcessIncomingMessageJobProcessor>();
            await processor.ProcessAsync(job, stoppingToken);
            await queue.DeleteMessageAsync(message.MessageId, message.PopReceipt, stoppingToken);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            logger.ZLogError(
                exception,
                $"IncomingMessageJobFailed message_id={message.MessageId} dequeue_count={message.DequeueCount} company_id={job?.CompanyId} conversation_id={job?.ConversationId} job_id={job?.JobId} correlation_id={job?.CorrelationId} trace_id={Activity.Current?.TraceId}");

            if (message.DequeueCount >= ProcessIncomingMessageJobRetryPolicy.MaxAttempts)
            {
                CeoAgentTelemetry.QueuePoisonCount.Add(1);
                await poisonQueue.SendMessageAsync(message.MessageText, stoppingToken);
                await queue.DeleteMessageAsync(message.MessageId, message.PopReceipt, stoppingToken);
            }
        }
        finally
        {
            stopwatch.Stop();
            CeoAgentTelemetry.QueueProcessingDuration.Record(stopwatch.ElapsedMilliseconds);
        }
    }

    private IDisposable? BeginJobScope(ProcessIncomingMessageJob job)
    {
        return logger.BeginScope(new Dictionary<string, object?>
        {
            ["correlation_id"] = job.CorrelationId,
            ["company_id"] = job.CompanyId,
            ["conversation_id"] = job.ConversationId,
            ["job_id"] = job.JobId,
            ["trace_id"] = Activity.Current?.TraceId.ToString(),
        });
    }
}
