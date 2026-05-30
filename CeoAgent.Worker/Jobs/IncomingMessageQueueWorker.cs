using System.Text.Json;
using Azure.Storage.Queues;
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
            var messages = await queue.ReceiveMessagesAsync(
                maxMessages: 1,
                visibilityTimeout: TimeSpan.FromMinutes(Math.Max(1, settings.VisibilityTimeoutMinutes)),
                cancellationToken: stoppingToken);

            if (messages.Value.Length == 0)
            {
                await Task.Delay(
                    TimeSpan.FromMilliseconds(Math.Max(100, settings.EmptyQueueDelayMilliseconds)),
                    stoppingToken);
                continue;
            }

            foreach (var message in messages.Value)
            {
                await ProcessSingleAsync(queue, poisonQueue, message, stoppingToken);
            }
        }
    }

    private async Task ProcessSingleAsync(
        QueueClient queue,
        QueueClient poisonQueue,
        Azure.Storage.Queues.Models.QueueMessage message,
        CancellationToken stoppingToken)
    {
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
                await poisonQueue.SendMessageAsync(message.MessageText, stoppingToken);
                await queue.DeleteMessageAsync(message.MessageId, message.PopReceipt, stoppingToken);
            }
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
