using CeoAgent.Application.Abstractions.Company;
using CeoAgent.Infrastructure.Implementation.Company;
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
    public async Task GetEnabledToolsAsync_ReturnsOnlyEnabledToolsForActiveCompanyWithSchemas()
    {
        await using var fixture = await RegistryFixture.CreateAsync();

        var tools = await fixture.Registry.GetEnabledToolsAsync(fixture.CompanyId, CancellationToken.None);

        tools.Select(tool => tool.Name).ShouldBe([MvpToolKeys.CheckGoogleCalendarAvailability]);
        var descriptor = tools.Single();
        descriptor.CompanyToolId.ShouldBe(fixture.EnabledToolId);
        descriptor.Description.ShouldBe("Check calendar safely.");
        descriptor.ParametersSchema.GetProperty("type").GetString().ShouldBe("object");
        descriptor.ParametersSchema.GetProperty("properties").TryGetProperty("fromCompanyTool", out _).ShouldBeTrue();
        descriptor.ParametersSchema.GetProperty("additionalProperties").GetBoolean().ShouldBeFalse();
    }

    private sealed class RegistryFixture : IAsyncDisposable
    {
        private readonly PostgresWorkerDatabase database;

        private RegistryFixture(PostgresWorkerDatabase database)
        {
            this.database = database;
            CompanyContext = database.CompanyContext;
            CompanyContext.SetCompany(CompanyId);
            DbContext = database.Context;
            Registry = new CompanyToolRegistry(DbContext);

            var company = new Company
            {
                Id = CompanyId,
                Name = "Contoso Bistro",
                TimeZoneId = "America/Bogota",
            };
            var otherCompany = new Company
            {
                Id = OtherCompanyId,
                Name = "Other Bistro",
                TimeZoneId = "America/Bogota",
            };

            DbContext.AddRange(
                company,
                otherCompany,
                new AgentProfile
                {
                    CompanyId = CompanyId,
                    ModelName = "gpt-4.1-mini",
                    DisplayName = "Contoso Assistant",
                    Language = "es",
                },
                new AgentProfile
                {
                    CompanyId = OtherCompanyId,
                    ModelName = "gpt-4.1-mini",
                    DisplayName = "Other Assistant",
                    Language = "es",
                },
                new CompanyTool
                {
                    Id = EnabledToolId,
                    CompanyId = CompanyId,
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
                    CompanyId = CompanyId,
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
                    CompanyId = OtherCompanyId,
                    ToolKey = MvpToolKeys.RequestHumanHandoff,
                    Description = "Other company tool.",
                    ParametersSchema = ParseSchema("""{"type":"object","properties":{},"required":[],"additionalProperties":false}"""),
                    IsEnabled = true,
                    Configuration = ToolConfiguration.ForRequestHumanHandoff(new RequestHumanHandoffConfig
                    {
                        TimeoutMinutes = 30,
                    }),
                });

            DbContext.SaveChanges();
        }

        public static async Task<RegistryFixture> CreateAsync()
        {
            return new RegistryFixture(await PostgresWorkerDatabase.CreateAsync());
        }

        public Guid CompanyId { get; } = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30");

        public Guid OtherCompanyId { get; } = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b50");

        public Guid EnabledToolId { get; } = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b40");

        public CompanyContextAccessor CompanyContext { get; }

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
}
