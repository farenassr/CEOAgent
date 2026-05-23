using CEOAgent.Application.Company;
using CEOAgent.Application.Errors;
using CEOAgent.Infrastructure.Persistence;
using CEOAgent.Infrastructure.Persistence.Entities;
using CEOAgent.Infrastructure.Tools;
using CEOAgent.Shared.Enums;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Integration.Tests.Persistence;

public sealed class ToolExecutionGatewayTests
{
    [Test]
    public async Task CreatePendingExecutionAsync_WhenCompanyToolBelongsToDifferentCompany_ThrowsBusinessRuleException()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var companyContext = new CompanyContextAccessor();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .UseSnakeCaseNamingConvention()
            .Options;
        await using var dbContext = new AppDbContext(options, companyContext, TimeProvider.System);
        await dbContext.Database.EnsureCreatedAsync();

        var companyA = Guid.CreateVersion7();
        var companyB = Guid.CreateVersion7();
        var channelA = Guid.CreateVersion7();
        var profileA = Guid.CreateVersion7();
        var customerA = Guid.CreateVersion7();
        var conversationA = Guid.CreateVersion7();
        var triggerMessageA = Guid.CreateVersion7();
        var companyBTool = Guid.CreateVersion7();

        SeedCompanies(dbContext, companyA, companyB);
        dbContext.CompanyChannels.Add(new CompanyChannel
        {
            Id = channelA,
            CompanyId = companyA,
            Provider = "whatsapp_cloud",
            ProviderChannelId = "channel-a"
        });
        dbContext.AgentProfiles.Add(new AgentProfile
        {
            Id = profileA,
            CompanyId = companyA,
            ModelName = "gpt-4.1-mini",
            DisplayName = "Contoso Assistant",
            Language = "es"
        });
        dbContext.Customers.Add(new Customer
        {
            Id = customerA,
            CompanyId = companyA,
            CompanyChannelId = channelA,
            ExternalCustomerId = "customer-a"
        });
        dbContext.Conversations.Add(new Conversation
        {
            Id = conversationA,
            CompanyId = companyA,
            CustomerId = customerA,
            CompanyChannelId = channelA,
            AgentProfileId = profileA,
            LastMessageAt = DateTime.UtcNow
        });
        dbContext.Messages.Add(new Message
        {
            Id = triggerMessageA,
            CompanyId = companyA,
            ConversationId = conversationA,
            Role = MessageRole.Assistant,
            OccurredAt = DateTime.UtcNow
        });
        dbContext.CompanyTools.Add(new CompanyTool
        {
            Id = companyBTool,
            CompanyId = companyB,
            ToolKey = "request_human_handoff"
        });
        await dbContext.SaveChangesAsync();

        companyContext.SetCompany(companyA);
        var gateway = new ToolExecutionGateway(dbContext);

        var request = new CreateToolExecutionRequest(
            companyA,
            conversationA,
            companyBTool,
            triggerMessageA,
            "request_human_handoff",
            "handoff-1",
            null);

        var exception = await Should.ThrowAsync<BusinessRuleException>(
            gateway.CreatePendingExecutionAsync(request, CancellationToken.None));
        exception.Code.ShouldBe("company_tool_mismatch");
    }

    private static void SeedCompanies(AppDbContext dbContext, Guid companyA, Guid companyB)
    {
        dbContext.Companies.AddRange(
            new CEOAgent.Infrastructure.Persistence.Entities.Company
            {
                Id = companyA,
                Name = "Company A",
                TimeZoneId = "America/Bogota"
            },
            new CEOAgent.Infrastructure.Persistence.Entities.Company
            {
                Id = companyB,
                Name = "Company B",
                TimeZoneId = "America/Bogota"
            });
    }
}
