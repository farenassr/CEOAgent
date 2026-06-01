using CeoAgent.Adapters.GoogleCalendar.Service;
using CeoAgent.Adapters.Secrets;
using Google.Apis.Calendar.v3;
using Shouldly;

namespace CeoAgent.IntegrationTests.Integrations;

public sealed class GoogleCalendarAuthenticationTests
{
    [Test]
    public void ServiceFactory_UsesMinimumCalendarScopes()
    {
        GoogleCalendarServiceFactory.Scopes.ShouldBe(
        [
            CalendarService.ScopeConstants.CalendarFreebusy,
            CalendarService.ScopeConstants.CalendarEvents,
        ]);
    }

    [Test]
    public async Task ServiceFactory_ReadsFullServiceAccountJsonFromConfigSecretReference()
    {
        var secrets = new RecordingSecretValueProvider
        {
            SecretValue = "{}",
        };
        var factory = new GoogleCalendarServiceFactory(secrets);

        await Should.ThrowAsync<InvalidOperationException>(
            factory.CreateAsync("config://GoogleCalendar:ServiceAccountJson", CancellationToken.None));

        secrets.References.ShouldBe(["config://GoogleCalendar:ServiceAccountJson"]);
    }

    [Test]
    public async Task ServiceFactory_WhenCredentialMaterialIsJson_DoesNotReadSecretProvider()
    {
        var secrets = new RecordingSecretValueProvider
        {
            SecretValue = "{}",
        };
        var factory = new GoogleCalendarServiceFactory(secrets);

        await Should.ThrowAsync<InvalidOperationException>(
            factory.CreateAsync("{}", CancellationToken.None));

        secrets.References.ShouldBeEmpty();
    }

    [Test]
    public async Task ServiceFactory_PassesKeyVaultUriToSecretProvider()
    {
        var secrets = new RecordingSecretValueProvider
        {
            SecretValue = "{}",
        };
        var factory = new GoogleCalendarServiceFactory(secrets);
        const string reference = "https://kv-ceo-agent-dev.vault.azure.net/secrets/GoogleCalendarServiceAccount";

        await Should.ThrowAsync<InvalidOperationException>(
            factory.CreateAsync(reference, CancellationToken.None));

        secrets.References.ShouldBe([reference]);
    }

    private sealed class RecordingSecretValueProvider : ISecretValueProvider
    {
        public List<string> References { get; } = [];

        public string SecretValue { get; init; } = string.Empty;

        public Task<string> GetSecretValueAsync(
            string reference,
            CancellationToken cancellationToken)
        {
            References.Add(reference);
            return Task.FromResult(SecretValue);
        }
    }
}
