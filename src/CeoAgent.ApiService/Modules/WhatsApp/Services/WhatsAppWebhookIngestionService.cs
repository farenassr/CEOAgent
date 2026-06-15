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
public sealed partial class WhatsAppWebhookIngestionService(
    CeoAgentDbContext dbContext,
    IncomingMessageOutboxDispatcher incomingMessageOutboxDispatcher,
    TimeProvider timeProvider,
    ILogger<WhatsAppWebhookIngestionService> logger)
{
    /// <summary>
    /// Resolves the target channel and conversation, stores the incoming WhatsApp message once, and queues agent processing.
    /// </summary>
    public async Task<WhatsAppWebhookIngestionResult> IngestAsync(
        string requestBody,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        WhatsAppWebhookIngestionStarted(
            logger,
            correlationId,
            requestBody.Length);

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(requestBody);
        }
        catch (JsonException)
        {
            WhatsAppWebhookInvalidJson(
                logger,
                correlationId,
                requestBody.Length);

            throw new InvalidWhatsAppWebhookPayloadException();
        }

        using (document)
        {
            var messages = Parse(document.RootElement);
            if (messages.Count == 0)
            {
                WhatsAppWebhookMessageNotFound(
                    logger,
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
        WhatsAppWebhookMessageParsed(
            logger,
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
            .Select(entity => new WebhookChannelContext(entity.Id, entity.OrganizationId))
            .SingleOrDefaultAsync(
                cancellationToken);
        if (channel is null)
        {
            WhatsAppWebhookUnknownChannel(
                logger,
                correlationId,
                message.PhoneNumberId,
                message.ProviderMessageId);

            return EmptyResult();
        }

        WhatsAppWebhookChannelResolved(
            logger,
            correlationId,
            channel.OrganizationId,
            channel.Id,
            message.PhoneNumberId);

        var existingMessage = await dbContext.Messages
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                entity => entity.OrganizationId == channel.OrganizationId
                    && entity.ProviderMessageId == message.ProviderMessageId,
                cancellationToken);

        if (existingMessage is not null)
        {
            WhatsAppWebhookDuplicateMessage(
                logger,
                correlationId,
                channel.OrganizationId,
                channel.Id,
                message.ProviderMessageId);

            return await HandleDuplicateInboundAsync(
                channel.OrganizationId,
                existingMessage.ConversationId,
                existingMessage.Id,
                correlationId,
                "preexisting",
                cancellationToken);
        }

        var customer = await ResolveCustomerAsync(channel, message, cancellationToken);
        var conversation = await ResolveConversationAsync(channel, customer, cancellationToken);
        var inbound = CreateInboundMessage(channel.OrganizationId, conversation.Id, message);
        var outbox = new IncomingMessageOutbox
        {
            OrganizationId = channel.OrganizationId,
            ConversationId = conversation.Id,
            MessageId = inbound.Id,
            CorrelationId = correlationId,
        };

        conversation.LastMessageAt = inbound.OccurredAt;
        dbContext.Messages.Add(inbound);
        dbContext.IncomingMessageOutbox.Add(outbox);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            var existing = await TryLoadDuplicateAsync(channel.OrganizationId, message.ProviderMessageId, cancellationToken);
            if (existing is null)
            {
                throw;
            }

            WhatsAppWebhookDuplicateMessage(
                logger,
                correlationId,
                channel.OrganizationId,
                channel.Id,
                message.ProviderMessageId);

            return await HandleDuplicateInboundAsync(
                channel.OrganizationId,
                existing.ConversationId,
                existing.Id,
                correlationId,
                "db_concurrency",
                cancellationToken);
        }

        WhatsAppWebhookMessagePersisted(
            logger,
            correlationId,
            channel.OrganizationId,
            channel.Id,
            conversation.Id,
            inbound.Id,
            outbox.Id,
            message.ProviderMessageId,
            message.Text?.Length ?? 0);

        var dispatched = await incomingMessageOutboxDispatcher.DispatchAsync(outbox.Id, cancellationToken);

        if (dispatched)
        {
            WhatsAppWebhookMessageEnqueued(
                logger,
                correlationId,
                channel.OrganizationId,
                channel.Id,
                conversation.Id,
                inbound.Id,
                message.ProviderMessageId,
                outbox.CorrelationId);
        }

        return new WhatsAppWebhookIngestionResult(
            Enqueued: dispatched,
            OrganizationId: channel.OrganizationId,
            ConversationId: conversation.Id,
            MessageId: inbound.Id);
    }

    private async Task<WhatsAppWebhookIngestionResult> HandleDuplicateInboundAsync(
        Guid organizationId,
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
                entity => entity.OrganizationId == organizationId
                    && entity.ConversationId == conversationId
                    && entity.Role == MessageRole.Assistant
                    && entity.ProviderMessageId == replyClientMessageId,
                cancellationToken);

        if (!hasReply)
        {
            WhatsAppWebhookDuplicateMessageRecoveryRequested(
                logger,
                organizationId,
                conversationId,
                messageId,
                reason);

            var dispatched = await EnsureOutboxAndDispatchAsync(
                organizationId,
                conversationId,
                messageId,
                correlationId,
                cancellationToken);

            return new WhatsAppWebhookIngestionResult(
                Enqueued: dispatched,
                OrganizationId: organizationId,
                ConversationId: conversationId,
                MessageId: messageId);
        }

        return new WhatsAppWebhookIngestionResult(
            Enqueued: false,
            OrganizationId: organizationId,
            ConversationId: conversationId,
            MessageId: messageId);
    }

    private async Task<bool> EnsureOutboxAndDispatchAsync(
        Guid organizationId,
        Guid conversationId,
        Guid messageId,
        string? correlationId,
        CancellationToken cancellationToken)
    {
        var outbox = await dbContext.IncomingMessageOutbox
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(
                entity => entity.OrganizationId == organizationId && entity.MessageId == messageId,
                cancellationToken);

        if (outbox is null)
        {
            outbox = new IncomingMessageOutbox
            {
                OrganizationId = organizationId,
                ConversationId = conversationId,
                MessageId = messageId,
                CorrelationId = correlationId,
            };
            dbContext.IncomingMessageOutbox.Add(outbox);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (outbox.Status == IncomingMessageOutboxStatus.Dispatched)
        {
            return false;
        }

        return await incomingMessageOutboxDispatcher.DispatchAsync(outbox.Id, cancellationToken);
    }

    private static WhatsAppWebhookIngestionResult EmptyResult()
    {
        return new WhatsAppWebhookIngestionResult(
            Enqueued: false,
            OrganizationId: null,
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
                entity => entity.OrganizationId == channel.OrganizationId
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
            OrganizationId = channel.OrganizationId,
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
                entity => entity.OrganizationId == channel.OrganizationId
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
            .Where(entity => entity.OrganizationId == channel.OrganizationId)
            .Select(entity => entity.Id)
            .SingleAsync(cancellationToken);

        conversation = new Conversation
        {
            OrganizationId = channel.OrganizationId,
            CustomerId = customer.Id,
            CompanyChannelId = channel.Id,
            AgentProfileId = agentProfileId,
            LastMessageAt = timeProvider.GetUtcNow().UtcDateTime,
        };
        dbContext.Conversations.Add(conversation);
        return conversation;
    }

    private async Task<DuplicateMessageContext?> TryLoadDuplicateAsync(
        Guid organizationId,
        string providerMessageId,
        CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();

        return await dbContext.Messages
            .IgnoreQueryFilters()
            .Where(entity => entity.OrganizationId == organizationId && entity.ProviderMessageId == providerMessageId)
            .Select(entity => new DuplicateMessageContext(entity.Id, entity.ConversationId))
            .SingleOrDefaultAsync(cancellationToken);
    }

    private static Message CreateInboundMessage(
        Guid organizationId,
        Guid conversationId,
        ParsedWhatsAppMessage message)
    {
        var inbound = new Message
        {
            OrganizationId = organizationId,
            ConversationId = conversationId,
            Role = MessageRole.User,
            Type = message.Type switch
            {
                "audio" => MessageType.Audio,
                "image" => MessageType.Image,
                _ => MessageType.Text,
            },
            MessageText = message.Text,
            ProviderMessageId = message.ProviderMessageId,
            Payload = new MessagePayload
            {
                ProviderType = message.Type,
                ProviderMessageId = message.ProviderMessageId,
                ProviderMediaId = message.MediaId,
                MimeType = message.MimeType,
                Sha256 = message.Sha256,
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
                    var media = ParseMedia(message, type);
                    parsed.Add(new ParsedWhatsAppMessage(
                        phoneNumberId.GetString() ?? string.Empty,
                        id.GetString() ?? string.Empty,
                        from.GetString() ?? string.Empty,
                        ContactName(value),
                        type,
                        text,
                        media.MediaId,
                        media.MimeType,
                        media.Sha256,
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

    private static ParsedMedia ParseMedia(JsonElement message, string type)
    {
        if (type is not ("image" or "audio")
            || !message.TryGetProperty(type, out var media)
            || media.ValueKind != JsonValueKind.Object)
        {
            return new ParsedMedia(null, null, null);
        }

        return new ParsedMedia(
            media.TryGetProperty("id", out var id) ? id.GetString() : null,
            media.TryGetProperty("mime_type", out var mimeType) ? mimeType.GetString() : null,
            media.TryGetProperty("sha256", out var sha256) ? sha256.GetString() : null);
    }

    private sealed record ParsedWhatsAppMessage(
        string PhoneNumberId,
        string ProviderMessageId,
        string From,
        string? ContactName,
        string Type,
        string? Text,
        string? MediaId,
        string? MimeType,
        string? Sha256,
        DateTime OccurredAtUtc);

    private sealed record ParsedMedia(string? MediaId, string? MimeType, string? Sha256);

    private sealed record WebhookChannelContext(Guid Id, Guid OrganizationId);

    private sealed record DuplicateMessageContext(Guid Id, Guid ConversationId);
}
