using System.Reflection;
using CeoAgent.Adapters;
using CeoAgent.Adapters.GoogleCalendar.Abstractions;
using CeoAgent.Adapters.GoogleCalendar;
using CeoAgent.Adapters.GoogleCalendar.Client;
using CeoAgent.Integrations.Calendar;
using Shouldly;

namespace CeoAgent.IntegrationTests.Integrations;

public sealed class GoogleCalendarRefitTests
{
    [Test]
    public void AdaptersProject_ReferencesRefitForGoogleCalendar()
    {
        typeof(AdaptersAssembly).Assembly
            .GetReferencedAssemblies()
            .Any(assembly => assembly.Name == "Refit")
            .ShouldBeTrue();
    }

    [Test]
    public void GoogleCalendarIntegrationContract_IsProviderNeutral()
    {
        typeof(ICalendarIntegration).GetMethod(nameof(ICalendarIntegration.CheckAvailabilityAsync)).ShouldNotBeNull();
        typeof(ICalendarIntegration).GetMethod(nameof(ICalendarIntegration.CreateReservationAsync)).ShouldNotBeNull();

        typeof(ICalendarIntegration)
            .Assembly
            .GetReferencedAssemblies()
            .Any(assembly => assembly.Name?.StartsWith("Google.Apis", StringComparison.Ordinal) == true)
            .ShouldBeFalse();
    }

    [Test]
    public void Adapter_UsesRefitClientFactory()
    {
        typeof(GoogleCalendarIntegration)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
            .SelectMany(constructor => constructor.GetParameters())
            .Any(parameter => parameter.ParameterType == typeof(IGoogleCalendarRefitClientFactory))
            .ShouldBeTrue();
    }

    [Test]
    public async Task CheckAvailabilityAsync_QueriesFreeBusyOnceForRequestedAndAlternativeRange()
    {
        var client = new RecordingGoogleCalendarRefitClient
        {
            FreeBusyResponse = new GoogleFreeBusyResponse(new Dictionary<string, GoogleCalendarBusyInfo>
            {
                ["primary"] = new GoogleCalendarBusyInfo(
                [
                    new GoogleBusyRange(
                        new DateTimeOffset(2026, 5, 28, 16, 0, 0, TimeSpan.FromHours(-5)),
                        new DateTimeOffset(2026, 5, 28, 17, 0, 0, TimeSpan.FromHours(-5))),
                    new GoogleBusyRange(
                        new DateTimeOffset(2026, 5, 28, 16, 30, 0, TimeSpan.FromHours(-5)),
                        new DateTimeOffset(2026, 5, 28, 17, 30, 0, TimeSpan.FromHours(-5))),
                ]),
            }),
        };
        var integration = new GoogleCalendarIntegration(new RecordingGoogleCalendarRefitClientFactory(client));

        var result = await integration.CheckAvailabilityAsync(
            new CalendarAvailabilityRequest(
                CredentialReference: "default",
                CalendarId: "primary",
                Start: new DateTimeOffset(2026, 5, 28, 16, 0, 0, TimeSpan.FromHours(-5)),
                End: new DateTimeOffset(2026, 5, 28, 17, 0, 0, TimeSpan.FromHours(-5)),
                PartySize: 2,
                AlternativeSearchStarts:
                [
                    new DateTimeOffset(2026, 5, 28, 16, 30, 0, TimeSpan.FromHours(-5)),
                    new DateTimeOffset(2026, 5, 28, 17, 30, 0, TimeSpan.FromHours(-5)),
                ]),
            CancellationToken.None);

        client.FreeBusyRequests.Count.ShouldBe(1);
        client.FreeBusyRequests.Single().TimeMin.ShouldBe(new DateTimeOffset(2026, 5, 28, 16, 0, 0, TimeSpan.FromHours(-5)));
        client.FreeBusyRequests.Single().TimeMax.ShouldBe(new DateTimeOffset(2026, 5, 28, 18, 30, 0, TimeSpan.FromHours(-5)));
        result.Available.ShouldBeFalse();
        result.AlternativeStarts.ShouldBe([new DateTimeOffset(2026, 5, 28, 17, 30, 0, TimeSpan.FromHours(-5))]);
    }

    private sealed class RecordingGoogleCalendarRefitClientFactory(IGoogleCalendarRefitClient client)
        : IGoogleCalendarRefitClientFactory
    {
        public Task<IGoogleCalendarRefitClient> CreateAsync(
            string credentialReference,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(client);
        }
    }

    private sealed class RecordingGoogleCalendarRefitClient : IGoogleCalendarRefitClient
    {
        public List<GoogleFreeBusyRequest> FreeBusyRequests { get; } = [];

        public GoogleFreeBusyResponse FreeBusyResponse { get; init; } = new(null);

        public Task<GoogleFreeBusyResponse> QueryFreeBusyAsync(
            GoogleFreeBusyRequest request,
            CancellationToken cancellationToken)
        {
            FreeBusyRequests.Add(request);
            return Task.FromResult(FreeBusyResponse);
        }

        public Task<GoogleCalendarEventResponse> CreateEventAsync(
            string calendarId,
            GoogleCalendarEventRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }
}
