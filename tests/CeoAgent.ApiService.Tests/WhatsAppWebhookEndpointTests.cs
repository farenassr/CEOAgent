using System.Net;
using System.Security.Cryptography;
using System.Text;
using CeoAgent.ApiService.Modules.WhatsApp;
using CeoAgent.ApiService.Tests.Support;
using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Application.Abstractions.Jobs;
using CeoAgent.Shared.Jobs;
using CeoAgent.Shared.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;

namespace CeoAgent.ApiService.Tests;

[NotInParallel]
public sealed class WhatsAppWebhookEndpointTests
{
    [Test]
    public void Constructor_DoesNotDependOnRawConfiguration()
    {
        typeof(WhatsAppWebhookEndpoint)
            .GetConstructors()
            .Single()
            .GetParameters()
            .Any(parameter => parameter.ParameterType == typeof(IConfiguration))
            .ShouldBeFalse();
    }

    [Test]
    public async Task PostWebhook_WhenSignedWithConfiguredAppSecret_AcceptsWebhook()
    {
        const string appSecret = "local-app-secret";
        var previousAppSecret = Environment.GetEnvironmentVariable("WhatsApp__AppSecret");
        try
        {
            Environment.SetEnvironmentVariable("WhatsApp__AppSecret", appSecret);

            var queue = new RecordingIncomingMessageQueue();
            await using var factory = new ApiFactory(configureServices: services =>
            {
                services.RemoveAll<IIncomingMessageJobEnqueuer>();
                services.AddSingleton<IIncomingMessageJobEnqueuer>(queue);
            });

            using (var scope = factory.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<CeoAgentDbContext>();
                SeedCompany(dbContext);
            }

            using var client = factory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/whatsapp")
            {
                Content = new StringContent(WebhookJson, Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("X-Hub-Signature-256", Sign(WebhookJson, appSecret));

            using var response = await client.SendAsync(request);

            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            queue.Jobs.Single().CompanyId.ShouldBe(CompanyId);
        }
        finally
        {
            Environment.SetEnvironmentVariable("WhatsApp__AppSecret", previousAppSecret);
        }
    }

    [Test]
    public async Task PostWebhook_WhenJsonIsMalformed_ReturnsBadRequest()
    {
        const string appSecret = "local-app-secret";
        var previousAppSecret = Environment.GetEnvironmentVariable("WhatsApp__AppSecret");
        try
        {
            Environment.SetEnvironmentVariable("WhatsApp__AppSecret", appSecret);

            await using var factory = new ApiFactory();
            using var client = factory.CreateClient();
            const string malformedJson = "{\"entry\":[";
            using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/whatsapp")
            {
                Content = new StringContent(malformedJson, Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("X-Hub-Signature-256", Sign(malformedJson, appSecret));

            using var response = await client.SendAsync(request);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }
        finally
        {
            Environment.SetEnvironmentVariable("WhatsApp__AppSecret", previousAppSecret);
        }
    }

    [Test]
    public async Task PostWebhook_WhenBodyExceedsConfiguredLimit_ReturnsPayloadTooLarge()
    {
        const string appSecret = "local-app-secret";
        var previousAppSecret = Environment.GetEnvironmentVariable("WhatsApp__AppSecret");
        var previousMaxBytes = Environment.GetEnvironmentVariable("WhatsApp__MaxWebhookBodyBytes");
        try
        {
            Environment.SetEnvironmentVariable("WhatsApp__AppSecret", appSecret);
            Environment.SetEnvironmentVariable("WhatsApp__MaxWebhookBodyBytes", "8");

            await using var factory = new ApiFactory();
            using var client = factory.CreateClient();
            const string body = "{\"entry\":[]}";
            using var request = new HttpRequestMessage(HttpMethod.Post, "/v1/whatsapp")
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            };
            request.Headers.Add("X-Hub-Signature-256", Sign(body, appSecret));

            using var response = await client.SendAsync(request);

            response.StatusCode.ShouldBe(HttpStatusCode.RequestEntityTooLarge);
        }
        finally
        {
            Environment.SetEnvironmentVariable("WhatsApp__AppSecret", previousAppSecret);
            Environment.SetEnvironmentVariable("WhatsApp__MaxWebhookBodyBytes", previousMaxBytes);
        }
    }

    private static string Sign(string requestBody, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(requestBody));
        return "sha256=" + Convert.ToHexString(hash).ToLowerInvariant();
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
            Id = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b32"),
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
            });

        dbContext.AddRange(company, profile, channel);
        dbContext.SaveChanges();
    }

    private static readonly Guid CompanyId = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30");

    private const string WebhookJson = """
        {
          "entry": [
            {
              "changes": [
                {
                  "value": {
                    "metadata": {
                      "phone_number_id": "1152556904604978"
                    },
                    "contacts": [
                      {
                        "wa_id": "15551234567",
                        "profile": { "name": "Ada" }
                      }
                    ],
                    "messages": [
                      {
                        "id": "wamid.endpoint",
                        "from": "15551234567",
                        "timestamp": "1779987600",
                        "type": "text",
                        "text": { "body": "Hola" }
                      }
                    ]
                  }
                }
              ]
            }
          ]
        }
        """;

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
