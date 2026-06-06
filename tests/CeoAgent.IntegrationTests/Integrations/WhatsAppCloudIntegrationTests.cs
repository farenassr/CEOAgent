using CeoAgent.Infrastructure.ApiClient.WhatsApp;
using CeoAgent.Infrastructure.Implementation.Messaging.WhatsApp;
using CeoAgent.Application.Abstractions.Secrets;
using CeoAgent.Infrastructure.Implementation.Secrets;
using CeoAgent.Application.Abstractions.Messaging;
using CeoAgent.Shared.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Shouldly;

namespace CeoAgent.IntegrationTests.Integrations;

public sealed class WhatsAppCloudIntegrationTests
{
    [Test]
    public async Task SecretValueProvider_WhenReferenceUsesConfigScheme_ReadsConfiguredUserSecretValue()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WhatsApp:AccessToken"] = "local-user-secret-token",
            })
            .Build();
        var provider = new SecretValueProvider(configuration, new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()));

        var value = await provider.GetSecretValueAsync(
            "config://WhatsApp:AccessToken",
            CancellationToken.None);

        value.ShouldBe("local-user-secret-token");
    }

    [Test]
    public async Task SendTextAsync_UsesCompanyChannelCredentialReferenceAsBearerToken()
    {
        var companyId = Guid.CreateVersion7();
        var channelId = Guid.CreateVersion7();
        var credentialReference = "https://kv-ceo-agent-dev.vault.azure.net/secrets/WhatsappAccessToken";
        var client = new RecordingWhatsAppCloudRefitClient();
        var resolver = new FakeWhatsAppCredentialResolver
        {
            Credential = new WhatsAppChannelCredentialReference(
                "1152556904604978",
                "840790722416204",
                credentialReference),
        };
        var secrets = new FakeSecretValueProvider
        {
            [credentialReference] = "token-from-key-vault",
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["WhatsApp:GraphApiBaseUrl"] = "https://graph.facebook.com/v99.0/",
            })
            .Build();
        var logger = new RecordingLogger<WhatsAppCloudIntegration>();
        var integration = new WhatsAppCloudIntegration(
            resolver,
            client,
            secrets,
            configuration,
            logger);

        await integration.SendTextAsync(
            new ChannelTextMessage(
                companyId,
                channelId,
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                "573001112233",
                "Hola",
                "reply:1"),
            CancellationToken.None);

        client.Authorization.ShouldBe("Bearer token-from-key-vault");
        client.PhoneNumberId.ShouldBe("1152556904604978");
        client.Request.ShouldNotBeNull();
        client.Request.To.ShouldBe("573001112233");
        client.Request.Text.ShouldNotBeNull();
        client.Request.Text.Body.ShouldBe("Hola");
        client.Request.BizOpaqueCallbackData.ShouldBe("reply:1");

        var log = logger.Entries.Single(entry => entry.EventId.Name == "WhatsAppCloudMessageSendStarting");
        log.Message.ShouldContain(companyId.ToString());
        log.Message.ShouldContain(channelId.ToString());
        log.Message.ShouldContain("Provider=whatsapp_cloud");
        log.Message.ShouldContain("HasIdempotencyKey=True");
        log.Message.ShouldNotContain("https://graph.facebook.com/v99.0/1152556904604978/messages");
        log.Message.ShouldNotContain("573001112233");
        log.Message.ShouldNotContain("reply:1");
        log.Message.ShouldNotContain("840790722416204");
        log.Message.ShouldNotContain("CredentialReference=https://kv-ceo-agent-dev.vault.azure.net/secrets/WhatsappAccessToken");
        log.Message.ShouldNotContain("AccessTokenLength=20");
        log.Message.ShouldNotContain("token-from-key-vault");
        log.Message.ShouldNotContain("Bearer token-from-key-vault");
    }

    private sealed class FakeWhatsAppCredentialResolver : IWhatsAppChannelCredentialResolver
    {
        public WhatsAppChannelCredentialReference? Credential { get; set; }

        public Task<WhatsAppChannelCredentialReference> ResolveAsync(
            Guid companyChannelId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(Credential ?? throw new InvalidOperationException("Credential was not configured."));
        }
    }

    private sealed class RecordingWhatsAppCloudRefitClient : IWhatsAppCloudClient
    {
        public string? Authorization { get; private set; }

        public string? PhoneNumberId { get; private set; }

        public WhatsAppSendMessageRequest? Request { get; private set; }

        public Task<WhatsAppSendMessageResponse> SendMessageAsync(
            string phoneNumberId,
            string authorization,
            WhatsAppSendMessageRequest request,
            CancellationToken cancellationToken)
        {
            PhoneNumberId = phoneNumberId;
            Authorization = authorization;
            Request = request;
            return Task.FromResult(new WhatsAppSendMessageResponse(
                [new WhatsAppSentMessage("wamid.sent")]));
        }

    }

    private sealed class FakeSecretValueProvider : Dictionary<string, string>, ISecretValueProvider
    {
        public Task<string> GetSecretValueAsync(string reference, CancellationToken cancellationToken)
        {
            return Task.FromResult(this[reference]);
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<LogEntry> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Entries.Add(new LogEntry(logLevel, eventId, formatter(state, exception)));
        }
    }

    private sealed record LogEntry(LogLevel LogLevel, EventId EventId, string Message);
}
