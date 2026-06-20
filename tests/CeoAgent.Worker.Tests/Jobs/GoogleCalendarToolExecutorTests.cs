using CeoAgent.Application.Abstractions.AITools;
using CeoAgent.Application.Abstractions.Organization;
using CeoAgent.Infrastructure.Implementation.Organization;
using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Application.Abstractions.AITools.GoogleCalendar;
using CeoAgent.Shared.Calendar;
using CeoAgent.Shared.Constants;
using CeoAgent.Shared.Enums;
using CeoAgent.Infrastructure.Implementation.AITools.Execution;
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
            CustomerName = "Ada Lovelace",
        };

        var result = await fixture.Executor.CreateReservationAsync(
            fixture.CreateExecutionContext("reservation-key"),
            request,
            CancellationToken.None);

        result.Status.ShouldBe(ToolExecutionStatus.ToolExecutionDenied);
        fixture.Calendar.ReservationRequests.ShouldBeEmpty();
        result.ToolKey.ShouldBe(MvpToolKeys.CreateGoogleCalendarReservation);
        result.FailureReason.ShouldBe("outside_working_hours");
    }

    [Test]
    public async Task CheckAvailabilityAsync_WhenRequestedSlotIsBusy_ReturnsNearbyFreeAlternativesFromSingleSearchWindow()
    {
        await using var fixture = await CalendarToolFixture.CreateAsync();
        fixture.Calendar.AvailableStarts.AddRange(
        [
            new DateTimeOffset(2026, 5, 28, 15, 30, 0, TimeSpan.FromHours(-5)),
            new DateTimeOffset(2026, 5, 28, 15, 0, 0, TimeSpan.FromHours(-5)),
            new DateTimeOffset(2026, 5, 28, 14, 30, 0, TimeSpan.FromHours(-5)),
            new DateTimeOffset(2026, 5, 28, 16, 30, 0, TimeSpan.FromHours(-5)),
            new DateTimeOffset(2026, 5, 28, 17, 0, 0, TimeSpan.FromHours(-5)),
            new DateTimeOffset(2026, 5, 28, 17, 30, 0, TimeSpan.FromHours(-5)),
        ]);

        var result = await fixture.Executor.CheckAvailabilityAsync(
            fixture.CreateExecutionContext("availability-key"),
            new CheckAvailabilityRequest
            {
                Date = new DateOnly(2026, 5, 28),
                PartySize = 2,
                PreferredTime = new TimeOnly(16, 0),
            },
            CancellationToken.None);

        result.Result!.CheckAvailability!.Available.ShouldBeFalse();
        result.Result.CheckAvailability.AlternativeSlots.ShouldBe(
        [
            new TimeOnly(15, 30),
            new TimeOnly(15, 0),
            new TimeOnly(14, 30),
            new TimeOnly(16, 30),
            new TimeOnly(17, 0),
            new TimeOnly(17, 30),
        ]);
        fixture.Calendar.AvailabilityRequests.Count.ShouldBe(1);
        var calendarRequest = fixture.Calendar.AvailabilityRequests.Single();
        calendarRequest.SearchWindowStart.ShouldBe(new DateTimeOffset(2026, 5, 28, 13, 0, 0, TimeSpan.FromHours(-5)));
        calendarRequest.SearchWindowEnd.ShouldBe(new DateTimeOffset(2026, 5, 28, 19, 0, 0, TimeSpan.FromHours(-5)));
        calendarRequest.AlternativeSearchStarts.ShouldBe(
        [
            new DateTimeOffset(2026, 5, 28, 15, 30, 0, TimeSpan.FromHours(-5)),
            new DateTimeOffset(2026, 5, 28, 15, 0, 0, TimeSpan.FromHours(-5)),
            new DateTimeOffset(2026, 5, 28, 14, 30, 0, TimeSpan.FromHours(-5)),
            new DateTimeOffset(2026, 5, 28, 16, 30, 0, TimeSpan.FromHours(-5)),
            new DateTimeOffset(2026, 5, 28, 17, 0, 0, TimeSpan.FromHours(-5)),
            new DateTimeOffset(2026, 5, 28, 17, 30, 0, TimeSpan.FromHours(-5)),
        ]);
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
            CustomerName = "Ada Lovelace",
        };

        await fixture.Executor.CreateReservationAsync(
            fixture.CreateExecutionContext("reservation-key"),
            request,
            CancellationToken.None);

        var second = await fixture.Executor.CreateReservationAsync(
            fixture.CreateExecutionContext("reservation-key"),
            request,
            CancellationToken.None);

        second.Status.ShouldBe(ToolExecutionStatus.ToolExecutionSucceeded);
        fixture.Calendar.ReservationRequests.Count.ShouldBe(1);
        await fixture.DbContext.SaveChangesAsync();
        (await fixture.DbContext.ToolExecutions.CountAsync()).ShouldBe(1);
    }

    [Test]
    public async Task CreateReservationAsync_WhenRequestedSlotIsBusy_DeniesWithoutCreatingCalendarEvent()
    {
        await using var fixture = await CalendarToolFixture.CreateAsync();
        fixture.Calendar.RequestedSlotAvailable = false;
        var request = new CreateCalendarEventRequest
        {
            Start = new DateTimeOffset(2026, 5, 28, 16, 0, 0, TimeSpan.FromHours(-5)),
            End = new DateTimeOffset(2026, 5, 28, 17, 0, 0, TimeSpan.FromHours(-5)),
            Summary = "Reservation for 2",
            CustomerName = "Ada Lovelace",
        };

        var result = await fixture.Executor.CreateReservationAsync(
            fixture.CreateExecutionContext("reservation-key"),
            request,
            CancellationToken.None);

        result.Status.ShouldBe(ToolExecutionStatus.ToolExecutionDenied);
        result.FailureReason.ShouldBe("slot_unavailable");
        fixture.Calendar.AvailabilityRequests.Count.ShouldBe(1);
        fixture.Calendar.ReservationRequests.ShouldBeEmpty();
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
            fixture.CreateExecutionContext("reservation-key"),
            new CreateCalendarEventRequest
            {
                Start = new DateTimeOffset(2026, 5, 28, 16, 0, 0, TimeSpan.FromHours(-5)),
                End = new DateTimeOffset(2026, 5, 28, 17, 0, 0, TimeSpan.FromHours(-5)),
                Summary = "Reservation for 2",
                CustomerName = "Ada Lovelace",
            },
            CancellationToken.None);

        fixture.Calendar.ReservationRequests.Single().CredentialReference.ShouldBe("stored://google-calendar/contoso");
    }

    [Test]
    public async Task CreateReservationAsync_StoresPrivateReservationMetadataFromCurrentConversationCustomer()
    {
        await using var fixture = await CalendarToolFixture.CreateAsync();

        await fixture.Executor.CreateReservationAsync(
            fixture.CreateExecutionContext("reservation-key"),
            new CreateCalendarEventRequest
            {
                Start = new DateTimeOffset(2026, 5, 28, 16, 0, 0, TimeSpan.FromHours(-5)),
                End = new DateTimeOffset(2026, 5, 28, 17, 0, 0, TimeSpan.FromHours(-5)),
                Summary = "Reservation for 2",
                CustomerName = "Ada Lovelace",
            },
            CancellationToken.None);

        var request = fixture.Calendar.ReservationRequests.Single();
        request.OrganizationId.ShouldBe(fixture.OrganizationId.ToString("D"));
        request.ConversationId.ShouldBe(fixture.Conversation.Id.ToString("D"));
        request.CustomerExternalId.ShouldBe("15551234567");
        request.CustomerName.ShouldBe("Ada Lovelace");
        request.CustomerPhoneNumber.ShouldBe("15551234567");
        request.ReservationId.ShouldBe("reservation-key");
    }

    [Test]
    public async Task FindReservationsAsync_UsesCurrentCustomerExternalIdAndCompanyLocalDateWindow()
    {
        await using var fixture = await CalendarToolFixture.CreateAsync();
        fixture.Calendar.FindResult = new CalendarReservationSearchResult(
        [
            new CalendarReservationInfo(
                "event-123",
                "event-123",
                new DateTimeOffset(2026, 5, 28, 16, 0, 0, TimeSpan.FromHours(-5)),
                new DateTimeOffset(2026, 5, 28, 17, 0, 0, TimeSpan.FromHours(-5)),
                "Reservation for 2",
                "Ada Lovelace",
                "https://calendar.google.com/event?eid=event-123",
                "15551234567"),
        ]);

        var execution = await fixture.Executor.FindReservationsAsync(
            fixture.CreateExecutionContext("find-key"),
            new FindGoogleCalendarReservationsRequest
            {
                Date = new DateOnly(2026, 5, 28),
                IncludePast = false,
                Status = null,
            },
            CancellationToken.None);

        var request = fixture.Calendar.FindRequests.Single();
        request.CustomerExternalId.ShouldBe("15551234567");
        request.TimeMin.ShouldBe(new DateTimeOffset(2026, 5, 28, 0, 0, 0, TimeSpan.FromHours(-5)));
        request.TimeMax.ShouldBe(new DateTimeOffset(2026, 5, 29, 0, 0, 0, TimeSpan.FromHours(-5)));
        execution.Result!.FindGoogleCalendarReservations!.Count.ShouldBe(1);
        execution.Result.FindGoogleCalendarReservations.DisambiguationNeeded.ShouldBeFalse();
    }

    [Test]
    public async Task FindReservationsAsync_WhenCalendarProviderFails_PersistsFailedExecution()
    {
        await using var fixture = await CalendarToolFixture.CreateAsync();
        fixture.Calendar.FindFailureReason = "upstream_error";

        var execution = await fixture.Executor.FindReservationsAsync(
            fixture.CreateExecutionContext("find-failed-key"),
            new FindGoogleCalendarReservationsRequest
            {
                Date = new DateOnly(2026, 5, 28),
                IncludePast = false,
                Status = null,
            },
            CancellationToken.None);

        execution.Status.ShouldBe(ToolExecutionStatus.ToolExecutionFailed);
        execution.FailureReason.ShouldBe("upstream_error");
        execution.Result.ShouldBeNull();
        fixture.DbContext.ChangeTracker.Entries<ToolExecution>().ShouldNotBeEmpty();
    }

    [Test]
    public async Task UpdateReservationAsync_WhenCustomerDoesNotOwnReservation_DeniesWithoutCalendarUpdate()
    {
        await using var fixture = await CalendarToolFixture.CreateAsync();
        fixture.Calendar.UpdateResult = CalendarReservationMutationResult.NotOwned("event-123");

        var execution = await fixture.Executor.UpdateReservationAsync(
            fixture.CreateExecutionContext("update-key"),
            new UpdateGoogleCalendarReservationRequest
            {
                ReservationId = "event-123",
                NewStart = new DateTimeOffset(2026, 5, 28, 20, 0, 0, TimeSpan.FromHours(-5)),
                NewEnd = new DateTimeOffset(2026, 5, 28, 21, 0, 0, TimeSpan.FromHours(-5)),
                Summary = null,
                CustomerName = null,
            },
            CancellationToken.None);

        execution.Status.ShouldBe(ToolExecutionStatus.ToolExecutionDenied);
        execution.FailureReason.ShouldBe("reservation_not_found_or_not_owned");
        fixture.Calendar.UpdateRequests.Count.ShouldBe(1);
    }

    [Test]
    public async Task UpdateReservationAsync_WhenCalendarProviderFails_PersistsFailedExecution()
    {
        await using var fixture = await CalendarToolFixture.CreateAsync();
        fixture.Calendar.UpdateResult = CalendarReservationMutationResult.Failed("upstream_error");

        var execution = await fixture.Executor.UpdateReservationAsync(
            fixture.CreateExecutionContext("update-failed-key"),
            new UpdateGoogleCalendarReservationRequest
            {
                ReservationId = "event-123",
                NewStart = new DateTimeOffset(2026, 5, 28, 20, 0, 0, TimeSpan.FromHours(-5)),
                NewEnd = new DateTimeOffset(2026, 5, 28, 21, 0, 0, TimeSpan.FromHours(-5)),
                Summary = null,
                CustomerName = null,
            },
            CancellationToken.None);

        execution.Status.ShouldBe(ToolExecutionStatus.ToolExecutionFailed);
        execution.FailureReason.ShouldBe("upstream_error");
        execution.Result.ShouldBeNull();
    }

    [Test]
    public async Task CancelReservationAsync_WhenCompanyOrCustomerDoesNotMatch_DeniesWithoutSuccessfulCancel()
    {
        await using var fixture = await CalendarToolFixture.CreateAsync();
        fixture.Calendar.CancelResult = CalendarReservationCancellationResult.NotOwned("event-123");

        var execution = await fixture.Executor.CancelReservationAsync(
            fixture.CreateExecutionContext("cancel-key"),
            new CancelGoogleCalendarReservationRequest
            {
                ReservationId = "event-123",
                Reason = null,
            },
            CancellationToken.None);

        execution.Status.ShouldBe(ToolExecutionStatus.ToolExecutionDenied);
        execution.FailureReason.ShouldBe("reservation_not_found_or_not_owned");
        fixture.Calendar.CancelRequests.Count.ShouldBe(1);
    }

    [Test]
    public async Task CancelReservationAsync_WhenCalendarProviderFails_PersistsFailedExecution()
    {
        await using var fixture = await CalendarToolFixture.CreateAsync();
        fixture.Calendar.CancelResult = CalendarReservationCancellationResult.Failed("event-123", "upstream_error");

        var execution = await fixture.Executor.CancelReservationAsync(
            fixture.CreateExecutionContext("cancel-failed-key"),
            new CancelGoogleCalendarReservationRequest
            {
                ReservationId = "event-123",
                Reason = null,
            },
            CancellationToken.None);

        execution.Status.ShouldBe(ToolExecutionStatus.ToolExecutionFailed);
        execution.FailureReason.ShouldBe("upstream_error");
        execution.Result.ShouldBeNull();
    }

    [Test]
    public async Task UpdateReservationAsync_ForValidReservation_CallsCalendarAndPersistsToolExecution()
    {
        await using var fixture = await CalendarToolFixture.CreateAsync();

        var execution = await fixture.Executor.UpdateReservationAsync(
            fixture.CreateExecutionContext("update-key"),
            new UpdateGoogleCalendarReservationRequest
            {
                ReservationId = "event-123",
                NewStart = new DateTimeOffset(2026, 5, 28, 20, 0, 0, TimeSpan.FromHours(-5)),
                NewEnd = new DateTimeOffset(2026, 5, 28, 21, 0, 0, TimeSpan.FromHours(-5)),
                Summary = null,
                CustomerName = null,
            },
            CancellationToken.None);

        execution.Status.ShouldBe(ToolExecutionStatus.ToolExecutionSucceeded);
        execution.ToolKey.ShouldBe(MvpToolKeys.UpdateGoogleCalendarReservation);
        fixture.Calendar.UpdateRequests.Count.ShouldBe(1);
        fixture.DbContext.ChangeTracker.Entries<ToolExecution>().ShouldNotBeEmpty();
    }

    [Test]
    public async Task CancelReservationAsync_ForValidReservation_CallsCalendarAndPersistsToolExecution()
    {
        await using var fixture = await CalendarToolFixture.CreateAsync();

        var execution = await fixture.Executor.CancelReservationAsync(
            fixture.CreateExecutionContext("cancel-key"),
            new CancelGoogleCalendarReservationRequest
            {
                ReservationId = "event-123",
                Reason = null,
            },
            CancellationToken.None);

        execution.Status.ShouldBe(ToolExecutionStatus.ToolExecutionSucceeded);
        execution.ToolKey.ShouldBe(MvpToolKeys.CancelGoogleCalendarReservation);
        fixture.Calendar.CancelRequests.Count.ShouldBe(1);
        fixture.DbContext.ChangeTracker.Entries<ToolExecution>().ShouldNotBeEmpty();
    }

    [Test]
    public async Task CreateReservationAsync_DoesNotTrackReadOnlyContextEntities()
    {
        await using var fixture = await CalendarToolFixture.CreateAsync();
        fixture.DbContext.ChangeTracker.Clear();

        await fixture.Executor.CreateReservationAsync(
            fixture.CreateExecutionContext("reservation-key"),
            new CreateCalendarEventRequest
            {
                Start = new DateTimeOffset(2026, 5, 28, 16, 0, 0, TimeSpan.FromHours(-5)),
                End = new DateTimeOffset(2026, 5, 28, 17, 0, 0, TimeSpan.FromHours(-5)),
                Summary = "Reservation for 2",
                CustomerName = "Ada Lovelace",
            },
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
            OrganizationContext = database.OrganizationContext;
            OrganizationContext.SetOrganization(OrganizationId);
            DbContext = database.Context;
            Calendar = new FakeCalendarIntegration();
            Executor = new GoogleCalendarToolExecutor(
                DbContext,
                Calendar,
                new FixedTimeProvider(new DateTimeOffset(2026, 5, 27, 12, 0, 0, TimeSpan.Zero)));

            var company = new Company
            {
                Id = OrganizationId,
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
                credentialReferenceId: null);

            var customer = new Customer
            {
                Id = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b33"),
                OrganizationId = OrganizationId,
                CompanyChannelId = channel.Id,
                ExternalCustomerId = "15551234567",
            };

            Conversation = new Conversation
            {
                Id = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b34"),
                OrganizationId = OrganizationId,
                CustomerId = customer.Id,
                CompanyChannelId = channel.Id,
                AgentProfileId = profile.Id,
                LastMessageAt = new DateTime(2026, 5, 28, 21, 0, 0, DateTimeKind.Utc),
            };

            TriggerMessage = new Message
            {
                Id = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b36"),
                OrganizationId = OrganizationId,
                ConversationId = Conversation.Id,
                Role = MessageRole.Assistant,
                Type = MessageType.Text,
                MessageText = "tool call",
                OccurredAt = new DateTime(2026, 5, 28, 21, 0, 0, DateTimeKind.Utc),
            };

            var credential = new IntegrationCredentialReference
            {
                Id = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b41"),
                OrganizationId = OrganizationId,
                Provider = IntegrationProvider.GoogleCalendar,
                Purpose = "google_calendar",
                Reference = "default",
            };

            Tool = new CompanyTool
            {
                Id = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b40"),
                OrganizationId = OrganizationId,
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
        }

        public static async Task<CalendarToolFixture> CreateAsync()
        {
            var fixture = new CalendarToolFixture(await PostgresWorkerDatabase.CreateAsync());
            await fixture.DbContext.SaveChangesAsync();
            return fixture;
        }

        public Guid OrganizationId { get; } = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30");

        public OrganizationContextAccessor OrganizationContext { get; }

        public CeoAgentDbContext DbContext { get; }

        public FakeCalendarIntegration Calendar { get; }

        public GoogleCalendarToolExecutor Executor { get; }

        public Conversation Conversation { get; }

        public CompanyTool Tool { get; }

        public Message TriggerMessage { get; }

        public ToolExecutionContext CreateExecutionContext(string idempotencyKey)
        {
            return new ToolExecutionContext(
                OrganizationId,
                Conversation.Id,
                Tool.Id,
                TriggerMessage.Id,
                idempotencyKey,
                Tool.CredentialReferenceId,
                Tool.Configuration);
        }

        public async ValueTask DisposeAsync()
        {
            await database.DisposeAsync();
        }
    }

    private sealed class FakeCalendarIntegration : IGoogleCalendarIntegration
    {
        public List<DateTimeOffset> AvailableStarts { get; } = [];

        public bool RequestedSlotAvailable { get; set; } = true;

        public List<CalendarAvailabilityRequest> AvailabilityRequests { get; } = [];

        public List<CalendarReservationRequest> ReservationRequests { get; } = [];

        public List<CalendarReservationSearchRequest> FindRequests { get; } = [];

        public List<CalendarReservationUpdateRequest> UpdateRequests { get; } = [];

        public List<CalendarReservationCancellationRequest> CancelRequests { get; } = [];

        public CalendarReservationSearchResult FindResult { get; set; } = new([]);

        public string? FindFailureReason { get; set; }

        public CalendarReservationMutationResult UpdateResult { get; set; } =
            CalendarReservationMutationResult.Updated(new CalendarReservationInfo(
                "event-123",
                "event-123",
                new DateTimeOffset(2026, 5, 28, 20, 0, 0, TimeSpan.FromHours(-5)),
                new DateTimeOffset(2026, 5, 28, 21, 0, 0, TimeSpan.FromHours(-5)),
                "Reservation for 2",
                "Ada Lovelace",
                "https://calendar.google.com/event?eid=event-123",
                "15551234567"));

        public CalendarReservationCancellationResult CancelResult { get; set; } =
            CalendarReservationCancellationResult.Cancelled("event-123", "event-123");

        public Task<CalendarAvailabilityResult> CheckAvailabilityAsync(
            CalendarAvailabilityRequest request,
            CancellationToken cancellationToken)
        {
            AvailabilityRequests.Add(request);
            var requestedSlotAvailable = request.AlternativeSearchStarts.Count == 0
                ? RequestedSlotAvailable
                : AvailableStarts.Contains(request.Start);
            var alternatives = request.AlternativeSearchStarts
                .Where(start => AvailableStarts.Contains(start))
                .Take(GoogleCalendarSchedulingPolicy.MaxAlternativeStarts)
                .ToArray();

            return Task.FromResult(new CalendarAvailabilityResult(
                Available: requestedSlotAvailable,
                AlternativeStarts: alternatives,
                UnavailabilityReason: requestedSlotAvailable ? null : "slot_unavailable"));
        }

        public Task<CalendarReservationResult> CreateReservationAsync(
            CalendarReservationRequest request,
            CancellationToken cancellationToken)
        {
            ReservationRequests.Add(request);
            return Task.FromResult(new CalendarReservationResult("event-123", "https://calendar.google.com/event?eid=event-123"));
        }

        public Task<CalendarReservationSearchResult> FindReservationsAsync(
            CalendarReservationSearchRequest request,
            CancellationToken cancellationToken)
        {
            FindRequests.Add(request);
            if (FindFailureReason is not null)
            {
                return Task.FromResult(CalendarReservationSearchResult.Failed(FindFailureReason));
            }

            return Task.FromResult(FindResult);
        }

        public Task<CalendarReservationMutationResult> UpdateReservationAsync(
            CalendarReservationUpdateRequest request,
            CancellationToken cancellationToken)
        {
            UpdateRequests.Add(request);
            return Task.FromResult(UpdateResult);
        }

        public Task<CalendarReservationCancellationResult> CancelReservationAsync(
            CalendarReservationCancellationRequest request,
            CancellationToken cancellationToken)
        {
            CancelRequests.Add(request);
            return Task.FromResult(CancelResult);
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
