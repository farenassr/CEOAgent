using System.Net;
using System.Net.Http.Json;
using CeoAgent.ApiService.Modules.WhatsApp;
using CeoAgent.ApiService.Tests.Support;
using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Application.Abstractions.Jobs;
using CeoAgent.Shared.Enums;
using CeoAgent.Shared.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;

namespace CeoAgent.ApiService.Tests;

[NotInParallel]
public sealed class AdminWhatsAppInboundMessageEndpointTests
{
    [Test]
    public async Task ReceiveWhatsAppMessage_PersistsUserMessageAsWhatsAppAndEnqueuesWorkerJob()
    {
        var queue = new RecordingIncomingMessageQueue();
        await using var factory = new ApiFactory(configureServices: services =>
        {
            services.RemoveAll<IIncomingMessageJobEnqueuer>();
            services.AddSingleton<IIncomingMessageJobEnqueuer>(queue);
        });
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<CeoAgentDbContext>();
            await SeedCompanyAsync(dbContext);
        }

        using var client = factory.CreateAuthenticatedClient(OrganizationId);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/v1/admin/whatsapp")
        {
            Content = JsonContent.Create(new
            {
                messageText = "Necesito una mesa para dos manana",
                externalCustomerId = "573001112233",
            }),
        };

        using var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SimulationResponse>();
        body.ShouldNotBeNull();
        body.OrganizationId.ShouldBe(OrganizationId);
        body.Enqueued.ShouldBeTrue();
        queue.Jobs.Single().OrganizationId.ShouldBe(OrganizationId);
        queue.Jobs.Single().ConversationId.ShouldBe(body.ConversationId);
        queue.Jobs.Single().MessageId.ShouldBe(body.MessageId);
        queue.Jobs.Single().CorrelationId.ShouldNotBeNullOrWhiteSpace();

        using var verifyScope = factory.Services.CreateScope();
        var verifyDbContext = verifyScope.ServiceProvider.GetRequiredService<CeoAgentDbContext>();
        var message = await verifyDbContext.Messages
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(entity => entity.Id == body.MessageId);
        message.ShouldNotBeNull();
        message.MessageText.ShouldBe("Necesito una mesa para dos manana");
        message.ProviderMessageId.ShouldBeNull();
        message.Payload.ShouldNotBeNull();
        message.Payload.ProviderType.ShouldBe("whatsapp_cloud");
        message.Payload.ProviderMessageId.ShouldBeNull();
        var dispatch = await verifyDbContext.MessageDispatches
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(entity => entity.MessageId == body.MessageId);
        dispatch.ShouldNotBeNull();
        dispatch.OrganizationId.ShouldBe(OrganizationId);
        dispatch.ConversationId.ShouldBe(body.ConversationId);
        dispatch.Operation.ShouldBe(MessageDispatchOperation.InboundQueueDispatch);
        dispatch.Provider.ShouldBe("azure_queue");
        dispatch.Status.ShouldBe(MessageDispatchStatus.Succeeded);
        dispatch.CorrelationId.ShouldBe(queue.Jobs.Single().CorrelationId);
    }

    [Test]
    public async Task ReceiveWhatsAppMessage_WhenQueueDispatchFails_PersistsRecoverableIncomingDispatch()
    {
        var queue = new RecordingIncomingMessageQueue { FailNextEnqueue = true };
        await using var factory = new ApiFactory(configureServices: services =>
        {
            services.RemoveAll<IIncomingMessageJobEnqueuer>();
            services.AddSingleton<IIncomingMessageJobEnqueuer>(queue);
        });
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<CeoAgentDbContext>();
            await SeedCompanyAsync(dbContext);
        }

        using var client = factory.CreateAuthenticatedClient(OrganizationId);
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/v1/admin/whatsapp")
        {
            Content = JsonContent.Create(new
            {
                messageText = "Necesito una mesa para dos manana",
                externalCustomerId = "573001112233",
            }),
        };

        using var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SimulationResponse>();
        body.ShouldNotBeNull();
        body.Enqueued.ShouldBeFalse();
        queue.Jobs.ShouldBeEmpty();

        using var verifyScope = factory.Services.CreateScope();
        var verifyDbContext = verifyScope.ServiceProvider.GetRequiredService<CeoAgentDbContext>();
        var dispatch = await verifyDbContext.MessageDispatches
            .IgnoreQueryFilters()
            .SingleAsync(entity => entity.MessageId == body.MessageId);
        dispatch.Operation.ShouldBe(MessageDispatchOperation.InboundQueueDispatch);
        dispatch.Status.ShouldBe(MessageDispatchStatus.RetryScheduled);
        dispatch.AttemptCount.ShouldBe(1);
        dispatch.LastError.ShouldBe("Simulated queue outage.");
    }

    private static async Task SeedCompanyAsync(CeoAgentDbContext dbContext)
    {
        var company = new Company
        {
            Id = OrganizationId,
            Name = "Contoso Bistro",
            TimeZoneId = "America/Bogota",
        };
        var profile = new AgentProfile
        {
            Id = AgentProfileId,
            OrganizationId = OrganizationId,
            ModelName = "gpt-4.1-mini",
            DisplayName = "Contoso Assistant",
            Language = "es",
        };
        var channel = CompanyChannel.ForWhatsAppCloud(
            OrganizationId,
            "1152556904604978",
            new WhatsAppCloudMetadata
            {
                BusinessAccountId = "840790722416204",
                PhoneNumberId = "1152556904604978",
                DisplayPhoneNumber = "+15556497030",
            },
            id: ChannelId);

        dbContext.AddRange(company, profile, channel);
        await dbContext.SaveChangesAsync();
    }

    private static readonly Guid OrganizationId = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30");

    private static readonly Guid AgentProfileId = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b31");

    private static readonly Guid ChannelId = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b32");

    private sealed class SimulationResponse
    {
        public Guid OrganizationId { get; set; }

        public Guid ConversationId { get; set; }

        public Guid MessageId { get; set; }

        public bool Enqueued { get; set; }
    }

    private sealed class RecordingIncomingMessageQueue : IIncomingMessageJobEnqueuer
    {
        public List<ProcessIncomingMessageJob> Jobs { get; } = [];

        public bool FailNextEnqueue { get; set; }

        public Task EnqueueAsync(ProcessIncomingMessageJob job, CancellationToken cancellationToken)
        {
            if (FailNextEnqueue)
            {
                FailNextEnqueue = false;
                throw new InvalidOperationException("Simulated queue outage.");
            }

            Jobs.Add(job);
            return Task.CompletedTask;
        }
    }
}
