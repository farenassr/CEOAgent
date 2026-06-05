using System.Net;
using System.Net.Http.Json;
using CeoAgent.ApiService.Infrastructure.Security;
using CeoAgent.ApiService.Modules.WhatsApp;
using CeoAgent.ApiService.Tests.Support;
using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Integrations.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Shouldly;

namespace CeoAgent.ApiService.Tests;

[NotInParallel]
public sealed class AdminWhatsAppInboundMessageEndpointTests
{
    [Test]
    public async Task ReceiveWhatsAppMessage_PersistsUserMessageAsWhatsAppAndEnqueuesWorkerJob()
    {
        var queue = new RecordingIncomingMessageQueue();
        const string adminKey = "test-admin-key";
        await using var factory = new ApiFactory(configureServices: services =>
        {
            services.RemoveAll<IIncomingMessageJobEnqueuer>();
            services.AddSingleton<IIncomingMessageJobEnqueuer>(queue);
            services.Configure<AdminApiKeyOptions>(options =>
            {
                options.Key = adminKey;
            });
        });
        using (var scope = factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<CeoAgentDbContext>();
            SeedCompany(dbContext);
        }
        factory.Services.GetRequiredService<IOptions<AdminApiKeyOptions>>().Value.CompanyId = CompanyId;

        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/v1/admin/companies/{CompanyId}/whatsapp")
        {
            Content = JsonContent.Create(new
            {
                messageText = "Necesito una mesa para dos manana",
                externalCustomerId = "573001112233",
            }),
        };
        request.Headers.Add("X-Admin-Api-Key", adminKey);

        using var response = await client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SimulationResponse>();
        body.ShouldNotBeNull();
        body.CompanyId.ShouldBe(CompanyId);
        body.Enqueued.ShouldBeTrue();
        queue.Jobs.Single().CompanyId.ShouldBe(CompanyId);
        queue.Jobs.Single().ConversationId.ShouldBe(body.ConversationId);
        queue.Jobs.Single().MessageId.ShouldBe(body.MessageId);

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
    }

    private static void SeedCompany(CeoAgentDbContext dbContext)
    {
        var company = new Company
        {
            Id = CompanyId,
            Name = "Contoso Bistro",
            TimeZoneId = "America/Bogota",
        };
        var profile = new AgentProfile
        {
            Id = AgentProfileId,
            CompanyId = CompanyId,
            ModelName = "gpt-4.1-mini",
            DisplayName = "Contoso Assistant",
            Language = "es",
        };
        var channel = CompanyChannel.ForWhatsAppCloud(
            CompanyId,
            "1152556904604978",
            new WhatsAppCloudMetadata
            {
                BusinessAccountId = "840790722416204",
                PhoneNumberId = "1152556904604978",
                DisplayPhoneNumber = "+15556497030",
            },
            id: ChannelId);

        dbContext.AddRange(company, profile, channel);
        dbContext.SaveChanges();
    }

    private static readonly Guid CompanyId = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30");

    private static readonly Guid AgentProfileId = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b31");

    private static readonly Guid ChannelId = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b32");

    private sealed class SimulationResponse
    {
        public Guid CompanyId { get; set; }

        public Guid ConversationId { get; set; }

        public Guid MessageId { get; set; }

        public bool Enqueued { get; set; }
    }

    private sealed class RecordingIncomingMessageQueue : IIncomingMessageJobEnqueuer
    {
        public List<ProcessIncomingMessageJob> Jobs { get; } = [];

        public Task EnqueueAsync(ProcessIncomingMessageJob job, CancellationToken cancellationToken)
        {
            Jobs.Add(job);
            return Task.CompletedTask;
        }
    }
}
