using CeoAgent.Infrastructure.ApiClient.WhatsApp;
using CeoAgent.Infrastructure.Implementation.Messaging.WhatsApp;
using CeoAgent.Application.Abstractions.Secrets;
using CeoAgent.Infrastructure.Implementation.Secrets;
using CeoAgent.Application.Abstractions.Messaging;
using CeoAgent.Shared.Messaging;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Refit;
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
    public async Task SecretValueProvider_WhenReferenceUsesKvScheme_ReadsConfiguredSecretAlias()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Secrets:whatsapp:contoso:access-token"] = "local-kv-alias-token",
            })
            .Build();
        var provider = new SecretValueProvider(configuration, new Microsoft.Extensions.Caching.Memory.MemoryCache(new Microsoft.Extensions.Caching.Memory.MemoryCacheOptions()));

        var value = await provider.GetSecretValueAsync(
            "kv://whatsapp/contoso/access-token",
            CancellationToken.None);

        value.ShouldBe("local-kv-alias-token");
    }

    [Test]
    public async Task SendTextAsync_UsesCompanyChannelCredentialReferenceAsBearerToken()
    {
        var organizationId = Guid.CreateVersion7();
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
                organizationId,
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
        log.EventId.Id.ShouldBe(4103);
        log.Message.ShouldContain("IntegrationProvider=whatsapp_cloud");
        log.Message.ShouldContain("HasIdempotencyKey=True");
        log.ScopeValues["OrganizationId"].ShouldBe(organizationId);
        log.ScopeValues["CompanyChannelId"].ShouldBe(channelId);
        log.Message.ShouldNotContain("https://graph.facebook.com/v99.0/1152556904604978/messages");
        log.Message.ShouldNotContain("573001112233");
        log.Message.ShouldNotContain("reply:1");
        log.Message.ShouldNotContain("840790722416204");
        log.Message.ShouldNotContain("CredentialReference=https://kv-ceo-agent-dev.vault.azure.net/secrets/WhatsappAccessToken");
        log.Message.ShouldNotContain("AccessTokenLength=20");
        log.Message.ShouldNotContain("token-from-key-vault");
        log.Message.ShouldNotContain("Bearer token-from-key-vault");
    }

    [Test]
    public async Task SendImageAsync_UploadsPrivateImageAndSendsMediaIdMessage()
    {
        var organizationId = Guid.CreateVersion7();
        var channelId = Guid.CreateVersion7();
        const string credentialReference = "kv://whatsapp/contoso/access-token";
        var client = new RecordingWhatsAppCloudRefitClient
        {
            UploadedMediaId = "media-123",
        };
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
        var configuration = new ConfigurationBuilder().Build();
        var logger = new RecordingLogger<WhatsAppCloudIntegration>();
        var integration = new WhatsAppCloudIntegration(
            resolver,
            client,
            secrets,
            configuration,
            logger);

        await integration.SendImageAsync(
            new ChannelImageMessage(
                organizationId,
                channelId,
                Guid.CreateVersion7(),
                Guid.CreateVersion7(),
                "573001112233",
                [1, 2, 3, 4],
                "image/png",
                "qr.png",
                "Datos de pago",
                "payment:tool-execution-id"),
            CancellationToken.None);

        client.UploadedFileName.ShouldBe("qr.png");
        client.UploadedContentType.ShouldBe("image/png");
        client.UploadedMessagingProduct.ShouldBe("whatsapp");
        client.Request.ShouldNotBeNull();
        client.Request.Type.ShouldBe("image");
        client.Request.Image.ShouldNotBeNull();
        client.Request.Image.Id.ShouldBe("media-123");
        client.Request.Image.Link.ShouldBeNull();
        client.Request.Image.Caption.ShouldBe("Datos de pago");
        client.Request.BizOpaqueCallbackData.ShouldBe("payment:tool-execution-id");
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

        public string UploadedMediaId { get; init; } = "media-default";

        public string? UploadedMessagingProduct { get; private set; }

        public string? UploadedContentType { get; private set; }

        public string? UploadedFileName { get; private set; }

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

        public Task<WhatsAppUploadMediaResponse> UploadMediaAsync(
            string phoneNumberId,
            string authorization,
            string messagingProduct,
            StreamPart file,
            CancellationToken cancellationToken)
        {
            PhoneNumberId = phoneNumberId;
            Authorization = authorization;
            UploadedMessagingProduct = messagingProduct;
            UploadedContentType = file.ContentType;
            UploadedFileName = file.FileName;
            return Task.FromResult(new WhatsAppUploadMediaResponse(UploadedMediaId));
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
        private readonly Stack<IReadOnlyDictionary<string, object?>> _scopes = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            var values = state is IEnumerable<KeyValuePair<string, object?>> pairs
                ? pairs.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal)
                : new Dictionary<string, object?>(StringComparer.Ordinal);
            _scopes.Push(values);
            return new Scope(() => _scopes.Pop());
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
            var scopeValues = _scopes
                .Reverse()
                .SelectMany(scope => scope)
                .ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            Entries.Add(new LogEntry(logLevel, eventId, formatter(state, exception), scopeValues));
        }

        private sealed class Scope(Action dispose) : IDisposable
        {
            public void Dispose()
            {
                dispose();
            }
        }
    }

    private sealed record LogEntry(
        LogLevel LogLevel,
        EventId EventId,
        string Message,
        IReadOnlyDictionary<string, object?> ScopeValues);
}
