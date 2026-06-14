using CeoAgent.Application.Abstractions.AITools;
using CeoAgent.Application.Abstractions.Organization;
using CeoAgent.Infrastructure.Implementation.Organization;
using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using System.Text.Json;
using CeoAgent.Shared.Constants;
using CeoAgent.Shared.Enums;
using CeoAgent.Infrastructure.Implementation.AITools.Execution;
using CeoAgent.Shared.AITools;
using CeoAgent.Worker.Tests.Infrastructure;
using Shouldly;

namespace CeoAgent.Worker.Tests.Jobs;

public sealed class CompanyToolRegistryTests
{
    [Test]
    public void ParametersSchema_ForNullableProperties_IsOpenAIStrictCompatible()
    {
        var tool = new FakeAgentTool<OptionalRequest>(
            "optional_request_tool",
            "Tests optional request schema.",
            isMutating: false);

        var schema = tool.ParametersSchema;
        var properties = schema.GetProperty("properties");
        var required = schema.GetProperty("required")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ToArray();

        required.ShouldBe(["requiredText", "optionalText", "optionalTime"]);
        properties.GetProperty("optionalText").GetProperty("type")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ShouldBe(["string", "null"]);
        properties.GetProperty("optionalTime").GetProperty("type")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ShouldBe(["string", "null"]);
        properties.GetProperty("optionalTime").GetProperty("format").GetString().ShouldBe("time");
    }

    [Test]
    public async Task GetEnabledToolsAsync_ReturnsOnlyEnabledToolsForActiveCompanyWithSchemas()
    {
        await using var fixture = await RegistryFixture.CreateAsync();

        var tools = await fixture.Registry.GetEnabledToolsAsync(fixture.OrganizationId, CancellationToken.None);

        tools.Select(tool => tool.Name).ShouldBe([MvpToolKeys.CheckGoogleCalendarAvailability]);
        var descriptor = tools.Single();
        descriptor.CompanyToolId.ShouldBe(fixture.EnabledToolId);
        descriptor.Description.ShouldBe("Code-first check availability description.");
        descriptor.ParametersSchema.GetProperty("type").GetString().ShouldBe("object");
        descriptor.ParametersSchema.GetProperty("properties").TryGetProperty("fromCompanyTool", out _).ShouldBeFalse();
        descriptor.ParametersSchema.GetProperty("additionalProperties").GetBoolean().ShouldBeFalse();
    }

    [Test]
    public async Task GetEnabledToolsAsync_MarksReservationUpdateAndCancelAsMutating()
    {
        await using var fixture = await RegistryFixture.CreateAsync(includeReservationTools: true);

        var tools = await fixture.Registry.GetEnabledToolsAsync(fixture.OrganizationId, CancellationToken.None);

        tools.Single(tool => tool.Name == MvpToolKeys.FindGoogleCalendarReservations).IsMutating.ShouldBeFalse();
        tools.Single(tool => tool.Name == MvpToolKeys.UpdateGoogleCalendarReservation).IsMutating.ShouldBeTrue();
        tools.Single(tool => tool.Name == MvpToolKeys.CancelGoogleCalendarReservation).IsMutating.ShouldBeTrue();
    }

    [Test]
    public async Task GetEnabledToolsAsync_ExcludesEnabledCompanyToolWithoutCatalogImplementation()
    {
        await using var fixture = await RegistryFixture.CreateAsync(includeUnsupportedTool: true);

        var tools = await fixture.Registry.GetEnabledToolsAsync(fixture.OrganizationId, CancellationToken.None);

        tools.Select(tool => tool.Name).ShouldNotContain("unsupported_tool");
    }

    private sealed class RegistryFixture : IAsyncDisposable
    {
        private readonly PostgresWorkerDatabase database;

        private RegistryFixture(
            PostgresWorkerDatabase database,
            bool includeReservationTools,
            bool includeUnsupportedTool)
        {
            this.database = database;
            OrganizationContext = database.OrganizationContext;
            OrganizationContext.SetOrganization(OrganizationId);
            DbContext = database.Context;
            Registry = new CompanyToolRegistry(
                DbContext,
                new CompositeAgentToolCatalog(
                    [
                        new FakeAgentTool<FakeRequest>(
                            MvpToolKeys.CheckGoogleCalendarAvailability,
                            "Code-first check availability description.",
                            isMutating: false),
                        new FakeAgentTool<FakeRequest>(
                            MvpToolKeys.FindGoogleCalendarReservations,
                            "Code-first find reservations description.",
                            isMutating: false),
                        new FakeAgentTool<FakeRequest>(
                            MvpToolKeys.UpdateGoogleCalendarReservation,
                            "Code-first update reservations description.",
                            isMutating: true),
                        new FakeAgentTool<FakeRequest>(
                            MvpToolKeys.CancelGoogleCalendarReservation,
                            "Code-first cancel reservations description.",
                            isMutating: true),
                    ],
                    []));

            var company = new Company
            {
                Id = OrganizationId,
                Name = "Contoso Bistro",
                TimeZoneId = "America/Bogota",
            };
            var otherCompany = new Company
            {
                Id = OtherOrganizationId,
                Name = "Other Bistro",
                TimeZoneId = "America/Bogota",
            };

            DbContext.AddRange(
                company,
                otherCompany,
                new AgentProfile
                {
                    OrganizationId = OrganizationId,
                    ModelName = "gpt-4.1-mini",
                    DisplayName = "Contoso Assistant",
                    Language = "es",
                },
                new AgentProfile
                {
                    OrganizationId = OtherOrganizationId,
                    ModelName = "gpt-4.1-mini",
                    DisplayName = "Other Assistant",
                    Language = "es",
                },
                new CompanyTool
                {
                    Id = EnabledToolId,
                    OrganizationId = OrganizationId,
                    ToolKey = MvpToolKeys.CheckGoogleCalendarAvailability,
                    Description = "Check calendar safely.",
                    ParametersSchema = ParseSchema("""{"type":"object","properties":{"fromCompanyTool":{"type":"string"}},"required":["fromCompanyTool"],"additionalProperties":false}"""),
                    IsEnabled = true,
                    Configuration = ToolConfiguration.ForGoogleCalendar(new GoogleCalendarConfig
                    {
                        CalendarId = "primary",
                        TimeZoneId = "America/Bogota",
                    }),
                },
                new CompanyTool
                {
                    OrganizationId = OrganizationId,
                    ToolKey = MvpToolKeys.CreateGoogleCalendarReservation,
                    Description = "Create calendar reservations.",
                    ParametersSchema = ParseSchema("""{"type":"object","properties":{"start":{"type":"string"}},"required":["start"],"additionalProperties":false}"""),
                    IsEnabled = false,
                    Configuration = ToolConfiguration.ForGoogleCalendar(new GoogleCalendarConfig
                    {
                        CalendarId = "primary",
                        TimeZoneId = "America/Bogota",
                    }),
                },
                new CompanyTool
                {
                    OrganizationId = OtherOrganizationId,
                    ToolKey = MvpToolKeys.RequestHumanHandoff,
                    Description = "Other company tool.",
                    ParametersSchema = ParseSchema("""{"type":"object","properties":{},"required":[],"additionalProperties":false}"""),
                    IsEnabled = true,
                    Configuration = ToolConfiguration.ForRequestHumanHandoff(new RequestHumanHandoffConfig
                    {
                        TimeoutMinutes = 30,
                    }),
                });

            if (includeReservationTools)
            {
                DbContext.CompanyTools.AddRange(
                    new CompanyTool
                    {
                        OrganizationId = OrganizationId,
                        ToolKey = MvpToolKeys.FindGoogleCalendarReservations,
                        Description = "Find reservations.",
                        ParametersSchema = ParseSchema("""{"type":"object","properties":{"date":{"type":["string","null"]},"includePast":{"type":"boolean"},"status":{"type":["string","null"]}},"required":["date","includePast","status"],"additionalProperties":false}"""),
                        IsEnabled = true,
                    },
                    new CompanyTool
                    {
                        OrganizationId = OrganizationId,
                        ToolKey = MvpToolKeys.UpdateGoogleCalendarReservation,
                        Description = "Update reservations.",
                        ParametersSchema = ParseSchema("""{"type":"object","properties":{"reservationId":{"type":"string"},"newStart":{"type":"string"},"newEnd":{"type":"string"},"summary":{"type":["string","null"]},"customerName":{"type":["string","null"]}},"required":["reservationId","newStart","newEnd","summary","customerName"],"additionalProperties":false}"""),
                        IsEnabled = true,
                    },
                    new CompanyTool
                    {
                        OrganizationId = OrganizationId,
                        ToolKey = MvpToolKeys.CancelGoogleCalendarReservation,
                        Description = "Cancel reservations.",
                        ParametersSchema = ParseSchema("""{"type":"object","properties":{"reservationId":{"type":"string"},"reason":{"type":["string","null"]}},"required":["reservationId","reason"],"additionalProperties":false}"""),
                        IsEnabled = true,
                    });
            }

            if (includeUnsupportedTool)
            {
                DbContext.CompanyTools.Add(new CompanyTool
                {
                    OrganizationId = OrganizationId,
                    ToolKey = "unsupported_tool",
                    Description = "Tenant supplied unsupported tool.",
                    ParametersSchema = ParseSchema("""{"type":"object","properties":{},"required":[],"additionalProperties":false}"""),
                    IsEnabled = true,
                });
            }

            DbContext.SaveChanges();
        }

        public static async Task<RegistryFixture> CreateAsync(
            bool includeReservationTools = false,
            bool includeUnsupportedTool = false)
        {
            return new RegistryFixture(
                await PostgresWorkerDatabase.CreateAsync(),
                includeReservationTools,
                includeUnsupportedTool);
        }

        public Guid OrganizationId { get; } = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30");

        public Guid OtherOrganizationId { get; } = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b50");

        public Guid EnabledToolId { get; } = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b40");

        public OrganizationContextAccessor OrganizationContext { get; }

        public CeoAgentDbContext DbContext { get; }

        public CompanyToolRegistry Registry { get; }

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

    private sealed class FakeAgentTool<TRequest>(
        string toolKey,
        string description,
        bool isMutating) : AgentTool<TRequest>
        where TRequest : class
    {
        public override string ToolKey => toolKey;

        public override bool IsMutating => isMutating;

        public override string Description => description;

        protected override Task<ToolExecution> ExecuteToolAsync(
            ToolExecutionContext context,
            TRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeRequest;

    private sealed class OptionalRequest
    {
        public required string RequiredText { get; init; }

        public string? OptionalText { get; init; }

        public TimeOnly? OptionalTime { get; init; }
    }
}
