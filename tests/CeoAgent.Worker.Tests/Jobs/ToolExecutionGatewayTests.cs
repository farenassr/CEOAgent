using System.Text.Json;
using CeoAgent.Integrations.AI;
using CeoAgent.Application.Company.Abstractions;
using CeoAgent.Application.Company.Implementation;
using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Integrations.Calendar;
using CeoAgent.Shared.Constants;
using CeoAgent.Shared.Enums;
using CeoAgent.Tools.Implementation.Execution;
using CeoAgent.Tools.Models.Execution;
using CeoAgent.Tools.Implementation.GoogleCalendar;
using CeoAgent.Worker.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace CeoAgent.Worker.Tests.Jobs;

public sealed class ToolExecutionGatewayTests
{
    [Test]
    public async Task ExecuteAsync_ForEnabledAvailabilityTool_ExecutesCalendarAndReturnsSanitizedToolResult()
    {
        await using var fixture = await GatewayFixture.CreateAsync();
        var tools = await fixture.Registry.GetEnabledToolsAsync(fixture.CompanyId, CancellationToken.None);
        var call = new AgentToolCall(
            "call-1",
            MvpToolKeys.CheckGoogleCalendarAvailability,
            JsonSerializer.SerializeToElement(new
            {
                date = "2026-05-28",
                partySize = 2,
                preferredTime = "16:00",
            }));

        var result = await fixture.Gateway.ExecuteAsync(
            new ToolExecutionGatewayRequest(
                fixture.CompanyId,
                fixture.Conversation.Id,
                fixture.TriggerMessage.Id,
                fixture.InboundMessage.Id,
                call,
                tools),
            CancellationToken.None);

        result.ToolCallId.ShouldBe("call-1");
        result.ToolName.ShouldBe(MvpToolKeys.CheckGoogleCalendarAvailability);
        result.Content.ShouldContain("\"status\":\"succeeded\"");
        fixture.Calendar.AvailabilityRequests.Count.ShouldBe(1);
        (await fixture.DbContext.ToolExecutions.CountAsync()).ShouldBe(1);
    }

    [Test]
    public async Task ExecuteAsync_ForDisabledTool_ReturnsDeniedResultWithoutSideEffect()
    {
        await using var fixture = await GatewayFixture.CreateAsync();
        var tools = await fixture.Registry.GetEnabledToolsAsync(fixture.CompanyId, CancellationToken.None);
        var call = new AgentToolCall(
            "call-2",
            MvpToolKeys.CreateGoogleCalendarReservation,
            JsonSerializer.SerializeToElement(new
            {
                start = "2026-05-28T16:00:00-05:00",
                end = "2026-05-28T17:00:00-05:00",
                summary = "Reservation for 2",
            }));

        var result = await fixture.Gateway.ExecuteAsync(
            new ToolExecutionGatewayRequest(
                fixture.CompanyId,
                fixture.Conversation.Id,
                fixture.TriggerMessage.Id,
                fixture.InboundMessage.Id,
                call,
                tools),
            CancellationToken.None);

        result.Content.ShouldContain("\"status\":\"denied\"");
        result.Content.ShouldContain("\"failureReason\":\"tool_not_enabled\"");
        fixture.Calendar.ReservationRequests.ShouldBeEmpty();
        (await fixture.DbContext.ToolExecutions.CountAsync()).ShouldBe(0);
    }

    [Test]
    public async Task ExecuteAsync_WhenSideEffectsAreDisabledForMutatingTool_DeniesWithoutSideEffect()
    {
        await using var fixture = await GatewayFixture.CreateAsync();
        var reservationTool = await fixture.DbContext.CompanyTools
            .SingleAsync(tool => tool.ToolKey == MvpToolKeys.CreateGoogleCalendarReservation);
        reservationTool.IsEnabled = true;
        await fixture.DbContext.SaveChangesAsync();

        var tools = await fixture.Registry.GetEnabledToolsAsync(fixture.CompanyId, CancellationToken.None);
        var call = new AgentToolCall(
            "call-reservation",
            MvpToolKeys.CreateGoogleCalendarReservation,
            JsonSerializer.SerializeToElement(new
            {
                start = "2026-05-28T16:00:00-05:00",
                end = "2026-05-28T17:00:00-05:00",
                summary = "Reservation for 2",
            }));

        var result = await fixture.Gateway.ExecuteAsync(
            new ToolExecutionGatewayRequest(
                fixture.CompanyId,
                fixture.Conversation.Id,
                fixture.TriggerMessage.Id,
                fixture.InboundMessage.Id,
                call,
                tools,
                SideEffectsEnabled: false),
            CancellationToken.None);

        result.Content.ShouldContain("\"status\":\"denied\"");
        result.Content.ShouldContain("\"failureReason\":\"side_effects_disabled\"");
        fixture.Calendar.ReservationRequests.ShouldBeEmpty();
    }

    [Test]
    public async Task ExecuteAsync_ForIncompleteAvailabilityArguments_ReturnsMalformedArgumentsWithoutSideEffect()
    {
        await using var fixture = await GatewayFixture.CreateAsync();
        var tools = await fixture.Registry.GetEnabledToolsAsync(fixture.CompanyId, CancellationToken.None);
        var call = new AgentToolCall(
            "call-malformed",
            MvpToolKeys.CheckGoogleCalendarAvailability,
            JsonSerializer.SerializeToElement(new
            {
                date = "2026-05-28",
            }));

        var result = await fixture.Gateway.ExecuteAsync(
            new ToolExecutionGatewayRequest(
                fixture.CompanyId,
                fixture.Conversation.Id,
                fixture.TriggerMessage.Id,
                fixture.InboundMessage.Id,
                call,
                tools),
            CancellationToken.None);

        result.Content.ShouldContain("\"status\":\"denied\"");
        result.Content.ShouldContain("\"failureReason\":\"malformed_arguments\"");
        fixture.Calendar.AvailabilityRequests.ShouldBeEmpty();
        (await fixture.DbContext.ToolExecutions.CountAsync()).ShouldBe(1);
    }

    [Test]
    public async Task ExecuteAsync_ForSameReservationArgumentsWithDifferentToolCallIds_CreatesOnlyOneCalendarEvent()
    {
        await using var fixture = await GatewayFixture.CreateAsync();
        var reservationTool = await fixture.DbContext.CompanyTools
            .SingleAsync(tool => tool.ToolKey == MvpToolKeys.CreateGoogleCalendarReservation);
        reservationTool.IsEnabled = true;
        await fixture.DbContext.SaveChangesAsync();

        var tools = await fixture.Registry.GetEnabledToolsAsync(fixture.CompanyId, CancellationToken.None);
        var arguments = new
        {
            start = "2026-05-28T16:00:00-05:00",
            end = "2026-05-28T17:00:00-05:00",
            summary = "Reservation for 2",
        };

        await fixture.Gateway.ExecuteAsync(
            new ToolExecutionGatewayRequest(
                fixture.CompanyId,
                fixture.Conversation.Id,
                fixture.TriggerMessage.Id,
                fixture.InboundMessage.Id,
                new AgentToolCall(
                    "call-reservation-1",
                    MvpToolKeys.CreateGoogleCalendarReservation,
                    JsonSerializer.SerializeToElement(arguments)),
                tools),
            CancellationToken.None);

        await fixture.Gateway.ExecuteAsync(
            new ToolExecutionGatewayRequest(
                fixture.CompanyId,
                fixture.Conversation.Id,
                fixture.TriggerMessage.Id,
                fixture.InboundMessage.Id,
                new AgentToolCall(
                    "call-reservation-2",
                    MvpToolKeys.CreateGoogleCalendarReservation,
                    JsonSerializer.SerializeToElement(arguments)),
                tools),
            CancellationToken.None);

        fixture.Calendar.ReservationRequests.Count.ShouldBe(1);
        (await fixture.DbContext.ToolExecutions.CountAsync()).ShouldBe(1);
    }

    private sealed class GatewayFixture : IAsyncDisposable
    {
        private readonly PostgresWorkerDatabase database;

        private GatewayFixture(PostgresWorkerDatabase database)
        {
            this.database = database;
            CompanyContext = database.CompanyContext;
            CompanyContext.SetCompany(CompanyId);
            DbContext = database.Context;
            Calendar = new FakeCalendarIntegration();
            Registry = new CompanyToolRegistry(DbContext);
            var executor = new GoogleCalendarToolExecutor(
                DbContext,
                Calendar,
                new FixedTimeProvider(new DateTimeOffset(2026, 5, 27, 12, 0, 0, TimeSpan.Zero)));
            Gateway = new ToolExecutionGateway(DbContext, executor);

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
                Id = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b35"),
                CompanyId = CompanyId,
                ConversationId = Conversation.Id,
                Role = MessageRole.Assistant,
                Type = MessageType.Text,
                MessageText = MvpToolKeys.CheckGoogleCalendarAvailability,
                OccurredAt = new DateTime(2026, 5, 28, 21, 1, 0, DateTimeKind.Utc),
            };
            InboundMessage = new Message
            {
                Id = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b36"),
                CompanyId = CompanyId,
                ConversationId = Conversation.Id,
                Role = MessageRole.User,
                Type = MessageType.Text,
                MessageText = "Quiero reservar a las cuatro.",
                OccurredAt = new DateTime(2026, 5, 28, 21, 0, 0, DateTimeKind.Utc),
            };
            var credential = new IntegrationCredentialReference
            {
                Id = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b41"),
                CompanyId = CompanyId,
                Provider = IntegrationProvider.GoogleCalendar,
                Purpose = "google_calendar",
                Reference = "config://GoogleCalendar:ServiceAccountJson",
            };

            DbContext.AddRange(
                company,
                profile,
                channel,
                customer,
                Conversation,
                TriggerMessage,
                InboundMessage,
                credential,
                new CompanyTool
                {
                    Id = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b40"),
                    CompanyId = CompanyId,
                    ToolKey = MvpToolKeys.CheckGoogleCalendarAvailability,
                    Description = "Check availability.",
                    ParametersSchema = ParseSchema("""{"type":"object","properties":{"date":{"type":"string"},"partySize":{"type":"integer"},"preferredTime":{"type":["string","null"]}},"required":["date","partySize","preferredTime"],"additionalProperties":false}"""),
                    IsEnabled = true,
                    CredentialReferenceId = credential.Id,
                    Configuration = ToolConfiguration.ForGoogleCalendar(new GoogleCalendarConfig
                    {
                        CalendarId = "primary",
                        TimeZoneId = "America/Bogota",
                    }),
                },
                new CompanyTool
                {
                    CompanyId = CompanyId,
                    ToolKey = MvpToolKeys.CreateGoogleCalendarReservation,
                    Description = "Create reservations.",
                    ParametersSchema = ParseSchema("""{"type":"object","properties":{"start":{"type":"string"},"end":{"type":"string"},"summary":{"type":"string"}},"required":["start","end","summary"],"additionalProperties":false}"""),
                    IsEnabled = false,
                    CredentialReferenceId = credential.Id,
                    Configuration = ToolConfiguration.ForGoogleCalendar(new GoogleCalendarConfig
                    {
                        CalendarId = "primary",
                        TimeZoneId = "America/Bogota",
                    }),
                });

            DbContext.SaveChanges();
        }

        public static async Task<GatewayFixture> CreateAsync()
        {
            return new GatewayFixture(await PostgresWorkerDatabase.CreateAsync());
        }

        public Guid CompanyId { get; } = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30");

        public CompanyContextAccessor CompanyContext { get; }

        public CeoAgentDbContext DbContext { get; }

        public FakeCalendarIntegration Calendar { get; }

        public CompanyToolRegistry Registry { get; }

        public ToolExecutionGateway Gateway { get; }

        public Conversation Conversation { get; }

        public Message TriggerMessage { get; }

        public Message InboundMessage { get; }

        public async ValueTask DisposeAsync()
        {
            await database.DisposeAsync();
        }

        private static JsonElement ParseSchema(string json)
        {
            using var document = JsonDocument.Parse(json);
            return document.RootElement.Clone();
        }
    }

    private sealed class FakeCalendarIntegration : ICalendarIntegration
    {
        public List<CalendarAvailabilityRequest> AvailabilityRequests { get; } = [];

        public List<CalendarReservationRequest> ReservationRequests { get; } = [];

        public Task<CalendarAvailabilityResult> CheckAvailabilityAsync(
            CalendarAvailabilityRequest request,
            CancellationToken cancellationToken)
        {
            AvailabilityRequests.Add(request);
            return Task.FromResult(new CalendarAvailabilityResult(true, [], null));
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
