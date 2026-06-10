using System.Text.Json;
using Azure.Storage.Queues;
using CeoAgent.Application;
using CeoAgent.Application.Abstractions.Jobs;
using CeoAgent.Shared.Jobs;
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
            try
            {
                var properties = await queue.GetPropertiesAsync(cancellationToken: stoppingToken);
                CeoAgentTelemetry.SetQueueBacklog(properties.Value.ApproximateMessagesCount);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to retrieve queue properties for backlog telemetry.");
            }

            Azure.Response<Azure.Storage.Queues.Models.QueueMessage[]> messages;
            try
            {
                messages = await queue.ReceiveMessagesAsync(
                    maxMessages: settings.MaxMessages > 0 ? settings.MaxMessages : 1,
                    visibilityTimeout: TimeSpan.FromMinutes(Math.Max(1, settings.VisibilityTimeoutMinutes)),
                    cancellationToken: stoppingToken);
                healthTracker.RecordPoll();
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Failed to receive messages from the incoming message queue.");
                await Task.Delay(
                    TimeSpan.FromMilliseconds(Math.Max(100, settings.EmptyQueueDelayMilliseconds)),
                    stoppingToken);
                continue;
            }

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

            await Parallel.ForEachAsync(messages.Value, parallelOptions, async (message, ct) => await ProcessSingleAsync(queue, poisonQueue, message, ct));
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
        var visibilityTimeout = TimeSpan.FromMinutes(Math.Max(1, options.Value.VisibilityTimeoutMinutes));
        var popReceipt = message.PopReceipt;
        using var renewalCancellation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var renewalTask = RenewVisibilityUntilCancelledAsync(
            queue,
            message.MessageId,
            message.MessageText,
            () => popReceipt,
            value => popReceipt = value,
            visibilityTimeout,
            renewalCancellation.Token);

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
            await queue.DeleteMessageAsync(message.MessageId, popReceipt, stoppingToken);
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
                await queue.DeleteMessageAsync(message.MessageId, popReceipt, stoppingToken);
            }
        }
        finally
        {
            await renewalCancellation.CancelAsync();
            await renewalTask;
            stopwatch.Stop();
            CeoAgentTelemetry.QueueProcessingDuration.Record(stopwatch.ElapsedMilliseconds);
        }
    }

    private async Task RenewVisibilityUntilCancelledAsync(
        QueueClient queue,
        string messageId,
        string messageText,
        Func<string> getPopReceipt,
        Action<string> setPopReceipt,
        TimeSpan visibilityTimeout,
        CancellationToken cancellationToken)
    {
        var renewalDelay = TimeSpan.FromMilliseconds(Math.Max(
            TimeSpan.FromSeconds(15).TotalMilliseconds,
            visibilityTimeout.TotalMilliseconds / 2));

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(renewalDelay, cancellationToken);
                var receipt = await queue.UpdateMessageAsync(
                    messageId,
                    getPopReceipt(),
                    messageText,
                    visibilityTimeout,
                    cancellationToken);
                setPopReceipt(receipt.Value.PopReceipt);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.ZLogWarning(
                    exception,
                    $"IncomingMessageVisibilityRenewalFailed message_id={messageId}");
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
