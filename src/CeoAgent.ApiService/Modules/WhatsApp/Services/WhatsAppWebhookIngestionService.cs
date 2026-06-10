using System.Globalization;
using System.Text.Json;
using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Application.Abstractions.Jobs;
using CeoAgent.Shared.Jobs;
using CeoAgent.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace CeoAgent.ApiService.Modules.WhatsApp;

/// <summary>
/// Parses WhatsApp webhook payloads, persists idempotent inbound messages, and enqueues background processing jobs.
/// </summary>
public sealed class WhatsAppWebhookIngestionService(
    CeoAgentDbContext dbContext,
    IIncomingMessageJobEnqueuer incomingMessageJobEnqueuer,
    TimeProvider timeProvider,
    ILogger<WhatsAppWebhookIngestionService> logger)
{
    private static readonly EventId WebhookIngestionStartedEvent = new(2101, "WhatsAppWebhookIngestionStarted");
    private static readonly EventId WebhookMessageNotFoundEvent = new(2102, "WhatsAppWebhookMessageNotFound");
    private static readonly EventId WebhookMessageParsedEvent = new(2103, "WhatsAppWebhookMessageParsed");
    private static readonly EventId WebhookChannelResolvedEvent = new(2104, "WhatsAppWebhookChannelResolved");
    private static readonly EventId WebhookDuplicateMessageEvent = new(2105, "WhatsAppWebhookDuplicateMessage");
    private static readonly EventId WebhookMessageEnqueuedEvent = new(2106, "WhatsAppWebhookMessageEnqueued");

    /// <summary>
    /// Resolves the target channel and conversation, stores the incoming WhatsApp message once, and queues agent processing.
    /// </summary>
    public async Task<WhatsAppWebhookIngestionResult> IngestAsync(
        string requestBody,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            WebhookIngestionStartedEvent,
            "WhatsAppWebhookIngestionStarted CorrelationId={CorrelationId} BodyLength={BodyLength}",
            correlationId,
            requestBody.Length);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(requestBody);
        }
        catch (JsonException)
        {
            logger.LogInformation(
                WebhookMessageNotFoundEvent,
                "WhatsAppWebhookInvalidJson CorrelationId={CorrelationId} BodyLength={BodyLength}",
                correlationId,
                requestBody.Length);

            throw new InvalidWhatsAppWebhookPayloadException();
        }

        using (document)
        {
            var messages = Parse(document.RootElement);
            if (messages.Count == 0)
            {
                logger.LogInformation(
                    WebhookMessageNotFoundEvent,
                    "WhatsAppWebhookMessageNotFound CorrelationId={CorrelationId} BodyLength={BodyLength}",
                    correlationId,
                    requestBody.Length);

                return EmptyResult();
            }

            WhatsAppWebhookIngestionResult? firstResult = null;
            var anyEnqueued = false;
            foreach (var message in messages)
            {
                var result = await IngestMessageAsync(message, correlationId, cancellationToken);
                firstResult ??= result;
                anyEnqueued |= result.Enqueued;
            }

            return firstResult is null
                ? EmptyResult()
                : firstResult with { Enqueued = anyEnqueued };
        }
    }

    private async Task<WhatsAppWebhookIngestionResult> IngestMessageAsync(
        ParsedWhatsAppMessage message,
        string? correlationId,
        CancellationToken cancellationToken)
    {

        logger.LogInformation(
            WebhookMessageParsedEvent,
            "WhatsAppWebhookMessageParsed CorrelationId={CorrelationId} PhoneNumberId={PhoneNumberId} ProviderMessageId={ProviderMessageId} FromLength={FromLength} HasContactName={HasContactName} MessageType={MessageType} TextLength={TextLength} OccurredAtUtc={OccurredAtUtc}",
            correlationId,
            message.PhoneNumberId,
            message.ProviderMessageId,
            message.From.Length,
            !string.IsNullOrWhiteSpace(message.ContactName),
            message.Type,
            message.Text?.Length,
            message.OccurredAtUtc);

        var channel = await dbContext.CompanyChannels
            .IgnoreQueryFilters()
            .Where(
                entity => entity.Provider == CompanyChannelProvider.WhatsAppCloud
                    && entity.ProviderChannelId == message.PhoneNumberId)
            .Select(entity => new WebhookChannelContext(entity.Id, entity.CompanyId))
            .SingleOrDefaultAsync(
                cancellationToken);
        if (channel is null)
        {
            logger.LogInformation(
                "WhatsAppWebhookUnknownChannel CorrelationId={CorrelationId} PhoneNumberId={PhoneNumberId} ProviderMessageId={ProviderMessageId}",
                correlationId,
                message.PhoneNumberId,
                message.ProviderMessageId);

            return EmptyResult();
        }

        logger.LogInformation(
            WebhookChannelResolvedEvent,
            "WhatsAppWebhookChannelResolved CorrelationId={CorrelationId} CompanyId={CompanyId} CompanyChannelId={CompanyChannelId} PhoneNumberId={PhoneNumberId} BusinessAccountId={BusinessAccountId}",
            correlationId,
            channel.CompanyId,
            channel.Id,
            message.PhoneNumberId,
            null);

        var existingMessage = await dbContext.Messages
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                entity => entity.CompanyId == channel.CompanyId
                    && entity.ProviderMessageId == message.ProviderMessageId,
                cancellationToken);

        if (existingMessage is not null)
        {
            logger.LogInformation(
                WebhookDuplicateMessageEvent,
                "WhatsAppWebhookDuplicateMessage CorrelationId={CorrelationId} CompanyId={CompanyId} CompanyChannelId={CompanyChannelId} ProviderMessageId={ProviderMessageId}",
                correlationId,
                channel.CompanyId,
                channel.Id,
                message.ProviderMessageId);

            return await HandleDuplicateInboundAsync(
                channel.CompanyId,
                existingMessage.ConversationId,
                existingMessage.Id,
                correlationId,
                "preexisting",
                cancellationToken);
        }

        var customer = await ResolveCustomerAsync(channel, message, cancellationToken);
        var conversation = await ResolveConversationAsync(channel, customer, cancellationToken);
        var inbound = CreateInboundMessage(channel.CompanyId, conversation.Id, message);

        conversation.LastMessageAt = inbound.OccurredAt;
        dbContext.Messages.Add(inbound);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            var existing = await TryLoadDuplicateAsync(channel.CompanyId, message.ProviderMessageId, cancellationToken);
            if (existing is null)
            {
                throw;
            }

            logger.LogInformation(
                WebhookDuplicateMessageEvent,
                "WhatsAppWebhookDuplicateMessage CorrelationId={CorrelationId} CompanyId={CompanyId} CompanyChannelId={CompanyChannelId} ProviderMessageId={ProviderMessageId}",
                correlationId,
                channel.CompanyId,
                channel.Id,
                message.ProviderMessageId);

            return await HandleDuplicateInboundAsync(
                channel.CompanyId,
                existing.ConversationId,
                existing.Id,
                correlationId,
                "db_concurrency",
                cancellationToken);
        }

        var job = new ProcessIncomingMessageJob(channel.CompanyId, conversation.Id, inbound.Id, correlationId);
        await incomingMessageJobEnqueuer.EnqueueAsync(job, cancellationToken);

        logger.LogInformation(
            WebhookMessageEnqueuedEvent,
            "WhatsAppWebhookMessageEnqueued CorrelationId={CorrelationId} CompanyId={CompanyId} CompanyChannelId={CompanyChannelId} ConversationId={ConversationId} MessageId={MessageId} ProviderMessageId={ProviderMessageId} QueueCorrelationId={QueueCorrelationId}",
            correlationId,
            channel.CompanyId,
            channel.Id,
            conversation.Id,
            inbound.Id,
            message.ProviderMessageId,
            job.CorrelationId);

        return new WhatsAppWebhookIngestionResult(
            Enqueued: true,
            CompanyId: channel.CompanyId,
            ConversationId: conversation.Id,
            MessageId: inbound.Id);
    }

    private async Task<WhatsAppWebhookIngestionResult> HandleDuplicateInboundAsync(
        Guid companyId,
        Guid conversationId,
        Guid messageId,
        string? correlationId,
        string reason,
        CancellationToken cancellationToken)
    {
        var replyClientMessageId = $"reply:{messageId}";
        var hasReply = await dbContext.Messages
            .IgnoreQueryFilters()
            .AnyAsync(
                entity => entity.CompanyId == companyId
                    && entity.ConversationId == conversationId
                    && entity.Role == MessageRole.Assistant
                    && entity.ProviderMessageId == replyClientMessageId,
                cancellationToken);

        if (!hasReply)
        {
            logger.LogInformation(
                "Re-enqueueing unprocessed duplicate webhook message. CompanyId={CompanyId} ConversationId={ConversationId} MessageId={MessageId} Reason={Reason}",
                companyId,
                conversationId,
                messageId,
                reason);

            var retryJob = new ProcessIncomingMessageJob(companyId, conversationId, messageId, correlationId);
            await incomingMessageJobEnqueuer.EnqueueAsync(retryJob, cancellationToken);

            return new WhatsAppWebhookIngestionResult(
                Enqueued: true,
                CompanyId: companyId,
                ConversationId: conversationId,
                MessageId: messageId);
        }

        return new WhatsAppWebhookIngestionResult(
            Enqueued: false,
            CompanyId: companyId,
            ConversationId: conversationId,
            MessageId: messageId);
    }

    private static WhatsAppWebhookIngestionResult EmptyResult()
    {
        return new WhatsAppWebhookIngestionResult(
            Enqueued: false,
            CompanyId: null,
            ConversationId: null,
            MessageId: null);
    }

    private async Task<Customer> ResolveCustomerAsync(
        WebhookChannelContext channel,
        ParsedWhatsAppMessage message,
        CancellationToken cancellationToken)
    {
        var customer = await dbContext.Customers
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(
                entity => entity.CompanyId == channel.CompanyId
                    && entity.CompanyChannelId == channel.Id
                    && entity.ExternalCustomerId == message.From,
                cancellationToken);

        if (customer is not null)
        {
            if (!string.IsNullOrWhiteSpace(message.ContactName))
            {
                customer.DisplayName = message.ContactName;
            }

            return customer;
        }

        customer = new Customer
        {
            CompanyId = channel.CompanyId,
            CompanyChannelId = channel.Id,
            ExternalCustomerId = message.From,
            DisplayName = message.ContactName,
        };
        dbContext.Customers.Add(customer);
        return customer;
    }

    private async Task<Conversation> ResolveConversationAsync(
        WebhookChannelContext channel,
        Customer customer,
        CancellationToken cancellationToken)
    {
        // Reuse an active conversation whether the bot is answering (Open) or a human is handling it
        // (HandedOff). Creating a new conversation during handoff would split the thread and could let the
        // bot answer on a fresh Open conversation while staff are still engaged on the same WhatsApp number.
        var conversation = await dbContext.Conversations
            .IgnoreQueryFilters()
            .Where(
                entity => entity.CompanyId == channel.CompanyId
                    && entity.CustomerId == customer.Id
                    && entity.CompanyChannelId == channel.Id
                    && (entity.Status == ConversationStatus.Open
                        || entity.Status == ConversationStatus.HandedOff))
            .OrderByDescending(entity => entity.LastMessageAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (conversation is not null)
        {
            return conversation;
        }

        var agentProfileId = await dbContext.AgentProfiles
            .IgnoreQueryFilters()
            .Where(entity => entity.CompanyId == channel.CompanyId)
            .Select(entity => entity.Id)
            .SingleAsync(cancellationToken);

        conversation = new Conversation
        {
            CompanyId = channel.CompanyId,
            CustomerId = customer.Id,
            CompanyChannelId = channel.Id,
            AgentProfileId = agentProfileId,
            LastMessageAt = timeProvider.GetUtcNow().UtcDateTime,
        };
        dbContext.Conversations.Add(conversation);
        return conversation;
    }

    private async Task<DuplicateMessageContext?> TryLoadDuplicateAsync(
        Guid companyId,
        string providerMessageId,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();

        return await dbContext.Messages
            .IgnoreQueryFilters()
            .Where(entity => entity.CompanyId == companyId && entity.ProviderMessageId == providerMessageId)
            .Select(entity => new DuplicateMessageContext(entity.Id, entity.ConversationId))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static Message CreateInboundMessage(
        Guid companyId,
        Guid conversationId,
        ParsedWhatsAppMessage message)
    {
        var inbound = new Message
        {
            CompanyId = companyId,
            ConversationId = conversationId,
            Role = MessageRole.User,
            Type = message.Type == "audio" ? MessageType.Audio : MessageType.Text,
            MessageText = message.Text,
            ProviderMessageId = message.ProviderMessageId,
            Payload = new MessagePayload
            {
                ProviderType = message.Type,
                ProviderMessageId = message.ProviderMessageId,
            },
            OccurredAt = message.OccurredAtUtc,
        };

        return inbound;
    }

    private static List<ParsedWhatsAppMessage> Parse(JsonElement root)
    {
        if (!root.TryGetProperty("entry", out var entries) || entries.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var parsed = new List<ParsedWhatsAppMessage>();
        foreach (var entry in entries.EnumerateArray())
        {
            if (!entry.TryGetProperty("changes", out var changes) || changes.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var change in changes.EnumerateArray())
            {
                if (!change.TryGetProperty("value", out var value)
                    || !value.TryGetProperty("messages", out var messages)
                    || messages.ValueKind != JsonValueKind.Array
                    || messages.GetArrayLength() == 0)
                {
                    continue;
                }

                if (!value.TryGetProperty("metadata", out var metadata)
                    || !metadata.TryGetProperty("phone_number_id", out var phoneNumberId))
                {
                    continue;
                }

                foreach (var message in messages.EnumerateArray())
                {
                    if (!message.TryGetProperty("id", out var id)
                        || !message.TryGetProperty("from", out var from))
                    {
                        continue;
                    }

                    var type = message.TryGetProperty("type", out var typeElement)
                        ? typeElement.GetString() ?? "text"
                        : "text";
                    var text = type == "text" && message.TryGetProperty("text", out var textElement)
                        && textElement.TryGetProperty("body", out var textBody)
                        ? textBody.GetString()
                        : null;
                    parsed.Add(new ParsedWhatsAppMessage(
                        phoneNumberId.GetString() ?? string.Empty,
                        id.GetString() ?? string.Empty,
                        from.GetString() ?? string.Empty,
                        ContactName(value),
                        type,
                        text,
                        OccurredAt(message)));
                }
            }
        }

        return parsed;
    }

    private static string? ContactName(JsonElement value)
    {
        if (!value.TryGetProperty("contacts", out var contacts) || contacts.GetArrayLength() == 0)
        {
            return null;
        }

        var contact = contacts[0];
        return contact.TryGetProperty("profile", out var profile) && profile.TryGetProperty("name", out var name)
            ? name.GetString()
            : null;
    }

    private static DateTime OccurredAt(JsonElement message)
    {
        if (!message.TryGetProperty("timestamp", out var timestamp)
            || !long.TryParse(timestamp.GetString(), NumberStyles.None, CultureInfo.InvariantCulture, out var unixSeconds))
        {
            return DateTime.UnixEpoch;
        }

        return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
    }

    private sealed record ParsedWhatsAppMessage(
        string PhoneNumberId,
        string ProviderMessageId,
        string From,
        string? ContactName,
        string Type,
        string? Text,
        DateTime OccurredAtUtc);

    private sealed record WebhookChannelContext(Guid Id, Guid CompanyId);

    private sealed record DuplicateMessageContext(Guid Id, Guid ConversationId);
}
