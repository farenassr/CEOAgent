using CeoAgent.ApiService.Modules.WhatsApp;
using CeoAgent.Application.Company.Abstractions;
using CeoAgent.Application.Company.Implementation;
using CeoAgent.Application.Errors;
using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Infrastructure.Persistence;
using CeoAgent.Integrations.Jobs;
using CeoAgent.Shared.Enums;
using CeoAgent.Shared.Request.AgentSimulation;
using CeoAgent.Shared.Response.AgentSimulation;
using FastEndpoints;
using FluentValidation;
using Microsoft.EntityFrameworkCore;

namespace CeoAgent.ApiService.Modules.AgentSimulation.Endpoints;

/// <summary>
/// Persists a synthetic user text message and enqueues the normal Worker flow.
/// </summary>
public sealed class SimulateIncomingMessageEndpoint(
    CeoAgentDbContext dbContext,
    ICompanyContext companyContext,
    IIncomingMessageJobEnqueuer incomingMessageJobEnqueuer,
    TimeProvider timeProvider) : Endpoint<AgentSimulationMessageRequest, AgentSimulationMessageResponse>
{
    private const string DefaultExternalCustomerId = "simulated-user";
    private const string ProviderType = "simulation";

    public override void Configure()
    {
        Post("/v1/admin/companies/{companyId}/agent-simulations/messages");
    }

    public override async Task HandleAsync(AgentSimulationMessageRequest request, CancellationToken cancellationToken)
    {
        var companyId = Route<Guid>("companyId");
        await EnsureCompanyIsAccessibleAsync(companyId, cancellationToken);

        var channel = await dbContext.CompanyChannels
            .WithDefaultTracking(trackChanges: true)
            .OrderBy(entity => entity.CreatedAt)
            .FirstOrDefaultAsync(entity => entity.CompanyId == companyId, cancellationToken)
            ?? throw new BusinessRuleException("company_channel_required", "Company requires at least one channel before agent simulation.");

        var customer = await ResolveCustomerAsync(companyId, channel.Id, request.ExternalCustomerId, cancellationToken);
        var conversation = await ResolveConversationAsync(companyId, channel.Id, customer.Id, cancellationToken);
        var providerMessageId = $"simulation:{Guid.CreateVersion7()}";
        var occurredAt = timeProvider.GetUtcNow().UtcDateTime;
        var inbound = new Message
        {
            CompanyId = companyId,
            ConversationId = conversation.Id,
            Role = MessageRole.User,
            Type = MessageType.Text,
            MessageText = request.MessageText,
            ProviderMessageId = providerMessageId,
            Payload = new MessagePayload
            {
                ProviderType = ProviderType,
                ProviderMessageId = providerMessageId,
            },
            OccurredAt = occurredAt,
        };

        conversation.LastMessageAt = occurredAt;
        dbContext.Messages.Add(inbound);
        await dbContext.SaveChangesAsync(cancellationToken);

        var job = new ProcessIncomingMessageJob(companyId, conversation.Id, inbound.Id, HttpContext.TraceIdentifier);
        await incomingMessageJobEnqueuer.EnqueueAsync(job, cancellationToken);

        await Send.OkAsync(
            new AgentSimulationMessageResponse
            {
                CompanyId = companyId,
                ConversationId = conversation.Id,
                MessageId = inbound.Id,
                Enqueued = true,
            },
            cancellationToken);
    }

    private async Task EnsureCompanyIsAccessibleAsync(Guid companyId, CancellationToken cancellationToken)
    {
        if (companyContext.CompanyId != companyId
            || !await dbContext.Companies
                .WithDefaultTracking()
                .AnyAsync(entity => entity.Id == companyId, cancellationToken))
        {
            throw new NotFoundException("company", companyId);
        }
    }

    private async Task<Customer> ResolveCustomerAsync(
        Guid companyId,
        Guid channelId,
        string? externalCustomerId,
        CancellationToken cancellationToken)
    {
        var normalizedExternalCustomerId = string.IsNullOrWhiteSpace(externalCustomerId)
            ? DefaultExternalCustomerId
            : externalCustomerId.Trim();
        var customer = await dbContext.Customers
            .WithDefaultTracking(trackChanges: true)
            .SingleOrDefaultAsync(
                entity => entity.CompanyId == companyId
                    && entity.CompanyChannelId == channelId
                    && entity.ExternalCustomerId == normalizedExternalCustomerId,
                cancellationToken);

        if (customer is not null)
        {
            return customer;
        }

        customer = new Customer
        {
            CompanyId = companyId,
            CompanyChannelId = channelId,
            ExternalCustomerId = normalizedExternalCustomerId,
            DisplayName = "Simulated User",
        };
        dbContext.Customers.Add(customer);
        return customer;
    }

    private async Task<Conversation> ResolveConversationAsync(
        Guid companyId,
        Guid channelId,
        Guid customerId,
        CancellationToken cancellationToken)
    {
        var conversation = await dbContext.Conversations
            .WithDefaultTracking(trackChanges: true)
            .SingleOrDefaultAsync(
                entity => entity.CompanyId == companyId
                    && entity.CustomerId == customerId
                    && entity.CompanyChannelId == channelId
                    && entity.Status == ConversationStatus.Open,
                cancellationToken);

        if (conversation is not null)
        {
            return conversation;
        }

        var agentProfileId = await dbContext.AgentProfiles
            .WithDefaultTracking()
            .Where(entity => entity.CompanyId == companyId)
            .Select(entity => entity.Id)
            .SingleOrDefaultAsync(cancellationToken);
        if (agentProfileId == Guid.Empty)
        {
            throw new BusinessRuleException("agent_profile_required", "Company requires an agent profile before agent simulation.");
        }

        conversation = new Conversation
        {
            CompanyId = companyId,
            CustomerId = customerId,
            CompanyChannelId = channelId,
            AgentProfileId = agentProfileId,
            LastMessageAt = timeProvider.GetUtcNow().UtcDateTime,
        };
        dbContext.Conversations.Add(conversation);
        return conversation;
    }
}

public sealed class AgentSimulationMessageValidator : Validator<AgentSimulationMessageRequest>
{
    public AgentSimulationMessageValidator()
    {
        RuleFor(request => request.MessageText).NotEmpty().MaximumLength(4000);
        RuleFor(request => request.ExternalCustomerId).MaximumLength(160);
    }
}
