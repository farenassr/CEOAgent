using CeoAgent.Application.Abstractions.Company;
using CeoAgent.Infrastructure.Implementation.Company;
using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Application.Abstractions.AITools.GoogleCalendar;
using CeoAgent.Shared.Calendar;
using CeoAgent.Shared.Constants;
using CeoAgent.Shared.Enums;
using CeoAgent.Infrastructure.Implementation.AITools.GoogleCalendar;
using CeoAgent.Worker.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace CeoAgent.Worker.Tests.Jobs;

public sealed class GoogleCalendarToolExecutorTests
{
    [Test]
    public async Task CreateReservationAsync_DoesNotCreateCalendarEventOutsideWorkingHours()
    {
        await using var fixture = await CalendarToolFixture.CreateAsync();
        var request = new CreateCalendarEventRequest
        {
            Start = new DateTimeOffset(2026, 5, 28, 8, 0, 0, TimeSpan.FromHours(-5)),
            End = new DateTimeOffset(2026, 5, 28, 9, 0, 0, TimeSpan.FromHours(-5)),
            Summary = "Reservation for 2",
        };

        var result = await fixture.Executor.CreateReservationAsync(
            fixture.CompanyId,
            fixture.Conversation.Id,
            fixture.Tool.Id,
            fixture.TriggerMessage.Id,
            request,
            "reservation-key",
            CancellationToken.None);

        result.Status.ShouldBe(ToolExecutionStatus.Denied);
        fixture.Calendar.ReservationRequests.ShouldBeEmpty();
        result.ToolKey.ShouldBe(MvpToolKeys.CreateGoogleCalendarReservation);
        result.FailureReason.ShouldBe("outside_working_hours");
    }

    [Test]
    public async Task CheckAvailabilityAsync_WhenRequestedSlotIsBusy_ReturnsNearestFreeAlternative()
    {
        await using var fixture = await CalendarToolFixture.CreateAsync();
        fixture.Calendar.AvailableStarts.Add(new DateTimeOffset(2026, 5, 28, 16, 30, 0, TimeSpan.FromHours(-5)));

        var result = await fixture.Executor.CheckAvailabilityAsync(
            fixture.CompanyId,
            fixture.Conversation.Id,
            fixture.Tool.Id,
            fixture.TriggerMessage.Id,
            new CheckAvailabilityRequest
            {
                Date = new DateOnly(2026, 5, 28),
                PartySize = 2,
                PreferredTime = new TimeOnly(16, 0),
            },
            "availability-key",
            CancellationToken.None);

        result.Result!.CheckAvailability!.Available.ShouldBeFalse();
        result.Result.CheckAvailability.AlternativeSlots.ShouldBe([new TimeOnly(16, 30)]);
        fixture.Calendar.AvailabilityRequests.Count.ShouldBe(1);
    }

    [Test]
    public async Task CreateReservationAsync_WithSameIdempotencyKey_CreatesOnlyOneCalendarEvent()
    {
        await using var fixture = await CalendarToolFixture.CreateAsync();
        var request = new CreateCalendarEventRequest
        {
            Start = new DateTimeOffset(2026, 5, 28, 16, 0, 0, TimeSpan.FromHours(-5)),
            End = new DateTimeOffset(2026, 5, 28, 17, 0, 0, TimeSpan.FromHours(-5)),
            Summary = "Reservation for 2",
        };

        await fixture.Executor.CreateReservationAsync(
            fixture.CompanyId,
            fixture.Conversation.Id,
            fixture.Tool.Id,
            fixture.TriggerMessage.Id,
            request,
            "reservation-key",
            CancellationToken.None);

        var second = await fixture.Executor.CreateReservationAsync(
            fixture.CompanyId,
            fixture.Conversation.Id,
            fixture.Tool.Id,
            fixture.TriggerMessage.Id,
            request,
            "reservation-key",
            CancellationToken.None);

        second.Status.ShouldBe(ToolExecutionStatus.Succeeded);
        fixture.Calendar.ReservationRequests.Count.ShouldBe(1);
        await fixture.DbContext.SaveChangesAsync();
        (await fixture.DbContext.ToolExecutions.CountAsync()).ShouldBe(1);
    }

    [Test]
    public async Task CreateReservationAsync_WithConfiguredCredentialReference_UsesStoredCredentialReference()
    {
        await using var fixture = await CalendarToolFixture.CreateAsync();
        var credential = await fixture.DbContext.IntegrationCredentialReferences.SingleAsync();
        credential.Reference = "stored://google-calendar/contoso";
        await fixture.DbContext.SaveChangesAsync();
        fixture.DbContext.ChangeTracker.Clear();

        await fixture.Executor.CreateReservationAsync(
            fixture.CompanyId,
            fixture.Conversation.Id,
            fixture.Tool.Id,
            fixture.TriggerMessage.Id,
            new CreateCalendarEventRequest
            {
                Start = new DateTimeOffset(2026, 5, 28, 16, 0, 0, TimeSpan.FromHours(-5)),
                End = new DateTimeOffset(2026, 5, 28, 17, 0, 0, TimeSpan.FromHours(-5)),
                Summary = "Reservation for 2",
            },
            "reservation-key",
            CancellationToken.None);

        fixture.Calendar.ReservationRequests.Single().CredentialReference.ShouldBe("stored://google-calendar/contoso");
    }

    [Test]
    public async Task CreateReservationAsync_DoesNotTrackReadOnlyContextEntities()
    {
        await using var fixture = await CalendarToolFixture.CreateAsync();
        fixture.DbContext.ChangeTracker.Clear();

        await fixture.Executor.CreateReservationAsync(
            fixture.CompanyId,
            fixture.Conversation.Id,
            fixture.Tool.Id,
            fixture.TriggerMessage.Id,
            new CreateCalendarEventRequest
            {
                Start = new DateTimeOffset(2026, 5, 28, 16, 0, 0, TimeSpan.FromHours(-5)),
                End = new DateTimeOffset(2026, 5, 28, 17, 0, 0, TimeSpan.FromHours(-5)),
                Summary = "Reservation for 2",
            },
            "reservation-key",
            CancellationToken.None);

        fixture.DbContext.ChangeTracker.Entries<Conversation>().ShouldBeEmpty();
        fixture.DbContext.ChangeTracker.Entries<Company>().ShouldBeEmpty();
        fixture.DbContext.ChangeTracker.Entries<CompanyTool>().ShouldBeEmpty();
        fixture.DbContext.ChangeTracker.Entries<IntegrationCredentialReference>().ShouldBeEmpty();
        fixture.DbContext.ChangeTracker.Entries<ToolExecution>().ShouldNotBeEmpty();
        fixture.DbContext.ChangeTracker.Entries<Message>().ShouldNotBeEmpty();
    }

    private sealed class CalendarToolFixture
    {
        private readonly PostgresWorkerDatabase database;

        private CalendarToolFixture(PostgresWorkerDatabase database)
        {
            this.database = database;
            CompanyContext = database.CompanyContext;
            CompanyContext.SetCompany(CompanyId);
            DbContext = database.Context;
            Calendar = new FakeCalendarIntegration();
            Executor = new GoogleCalendarToolExecutor(
                DbContext,
                Calendar,
                new FixedTimeProvider(new DateTimeOffset(2026, 5, 27, 12, 0, 0, TimeSpan.Zero)));

            var company = new Company
            {
                Id = CompanyId,
                Name = "Contoso Bistro",
                TimeZoneId = "America/Bogota",
                WorkingHours = new WorkingHours
                {
                    Schedule = new WeeklySchedule
                    {
                        Thursday =
                        [
                            new TimeSlot
                            {
                                Start = new TimeOnly(12, 0),
                                End = new TimeOnly(22, 0),
                            },
                        ],
                    },
                },
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
                },
                credentialReferenceId: null);

            var customer = new Customer
            {
                Id = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b33"),
                CompanyId = CompanyId,
                CompanyChannelId = channel.Id,
                ExternalCustomerId = "15551234567",
            };

            Conversation = new Conversation
            {
                Id = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b34"),
                CompanyId = CompanyId,
                CustomerId = customer.Id,
                CompanyChannelId = channel.Id,
                AgentProfileId = profile.Id,
                LastMessageAt = new DateTime(2026, 5, 28, 21, 0, 0, DateTimeKind.Utc),
            };

            TriggerMessage = new Message
            {
                Id = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b36"),
                CompanyId = CompanyId,
                ConversationId = Conversation.Id,
                Role = MessageRole.Assistant,
                Type = MessageType.Text,
                MessageText = "tool call",
                OccurredAt = new DateTime(2026, 5, 28, 21, 0, 0, DateTimeKind.Utc),
            };

            var credential = new IntegrationCredentialReference
            {
                Id = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b41"),
                CompanyId = CompanyId,
                Provider = IntegrationProvider.GoogleCalendar,
                Purpose = "google_calendar",
                Reference = "default",
            };

            Tool = new CompanyTool
            {
                Id = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b40"),
                CompanyId = CompanyId,
                ToolKey = MvpToolKeys.CreateGoogleCalendarReservation,
                CredentialReferenceId = credential.Id,
                Configuration = ToolConfiguration.ForGoogleCalendar(new GoogleCalendarConfig
                {
                    CalendarId = "primary",
                    TimeZoneId = "America/Bogota",
                    BufferMinutes = 0,
                }),
            };

            DbContext.AddRange(company, profile, channel, customer, Conversation, TriggerMessage, credential, Tool);
            DbContext.SaveChanges();
        }

        public static async Task<CalendarToolFixture> CreateAsync()
        {
            return new CalendarToolFixture(await PostgresWorkerDatabase.CreateAsync());
        }

        public Guid CompanyId { get; } = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30");

        public CompanyContextAccessor CompanyContext { get; }

        public CeoAgentDbContext DbContext { get; }

        public FakeCalendarIntegration Calendar { get; }

        public GoogleCalendarToolExecutor Executor { get; }

        public Conversation Conversation { get; }

        public CompanyTool Tool { get; }

        public Message TriggerMessage { get; }

        public async ValueTask DisposeAsync()
        {
            await database.DisposeAsync();
        }
    }

    private sealed class FakeCalendarIntegration : ICalendarIntegration
    {
        public List<DateTimeOffset> AvailableStarts { get; } = [];

        public List<CalendarAvailabilityRequest> AvailabilityRequests { get; } = [];

        public List<CalendarReservationRequest> ReservationRequests { get; } = [];

        public Task<CalendarAvailabilityResult> CheckAvailabilityAsync(
            CalendarAvailabilityRequest request,
            CancellationToken cancellationToken)
        {
            AvailabilityRequests.Add(request);
            var alternatives = request.AlternativeSearchStarts
                .Where(start => AvailableStarts.Contains(start))
                .Take(1)
                .ToArray();

            return Task.FromResult(new CalendarAvailabilityResult(
                Available: false,
                AlternativeStarts: alternatives,
                UnavailabilityReason: "slot_unavailable"));
        }

        public Task<CalendarReservationResult> CreateReservationAsync(
            CalendarReservationRequest request,
            CancellationToken cancellationToken)
        {
            ReservationRequests.Add(request);
            return Task.FromResult(new CalendarReservationResult("event-123", "https://calendar.google.com/event?eid=event-123"));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }
}
