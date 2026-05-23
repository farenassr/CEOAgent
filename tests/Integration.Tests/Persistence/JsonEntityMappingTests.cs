using CEOAgent.Application.Company;
using CEOAgent.Infrastructure.Persistence;
using CEOAgent.Infrastructure.Persistence.Entities;
using CEOAgent.Infrastructure.Persistence.Entities.Json;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using CompanyEntity = CEOAgent.Infrastructure.Persistence.Entities.Company;
using WorkingHoursEntity = CEOAgent.Infrastructure.Persistence.Entities.Json.WorkingHours;

namespace Integration.Tests.Persistence;

public sealed class JsonEntityMappingTests
{
    [Test]
    public void Model_MapsJsonbEntityProperties_AsTypedObjectsWithStableColumnNames()
    {
        using var dbContext = CEOAgentDbContextTestFactory.Create();
        var model = dbContext.Model;

        AssertJsonProperty<CompanyEntity, WorkingHoursEntity>(model, nameof(CompanyEntity.WorkingHours), "working_hours_json");
        AssertJsonProperty<CompanyChannel, ChannelMetadata>(model, nameof(CompanyChannel.Metadata), "metadata_json");
        AssertJsonProperty<CompanyTool, ToolConfiguration>(model, nameof(CompanyTool.Configuration), "configuration_json");
        AssertJsonProperty<ConversationState, ConversationStateSnapshot>(model, nameof(ConversationState.Snapshot), "state_json");
        AssertJsonProperty<IntegrationCredentialReference, CredentialMetadata>(model, nameof(IntegrationCredentialReference.Metadata), "metadata_json");
        AssertJsonProperty<Message, MessagePayload>(model, nameof(Message.Payload), "payload_json");
        AssertJsonProperty<ToolExecution, ToolExecutionRequest>(model, nameof(ToolExecution.Request), "request_json");
        AssertJsonProperty<ToolExecution, ToolExecutionResult>(model, nameof(ToolExecution.Result), "result_json");
    }

    [Test]
    public void JsonEntityTypes_ExposeExpectedPolymorphicDerivedTypes()
    {
        AssertAssignableTo<ChannelMetadata, WhatsAppCloudMetadata>();
        AssertAssignableTo<ChannelMetadata, InstagramMetadata>();
        AssertAssignableTo<ChannelMetadata, TelegramMetadata>();

        AssertAssignableTo<ToolConfiguration, CheckAvailabilityConfig>();
        AssertAssignableTo<ToolConfiguration, RequestHumanHandoffConfig>();
        AssertAssignableTo<ToolConfiguration, GoogleCalendarConfig>();

        AssertAssignableTo<CredentialMetadata, GoogleCalendarCredentialMetadata>();
        AssertAssignableTo<CredentialMetadata, WhatsAppCloudCredentialMetadata>();
        AssertAssignableTo<CredentialMetadata, GenericOAuthCredentialMetadata>();

        AssertAssignableTo<MessagePayload, TextPayload>();
        AssertAssignableTo<MessagePayload, MediaPayload>();
        AssertAssignableTo<MessagePayload, InteractivePayload>();
        AssertAssignableTo<MessagePayload, LocationPayload>();

        AssertAssignableTo<ToolExecutionRequest, CheckAvailabilityRequest>();
        AssertAssignableTo<ToolExecutionRequest, RequestHumanHandoffRequest>();
        AssertAssignableTo<ToolExecutionRequest, CreateCalendarEventRequest>();

        AssertAssignableTo<ToolExecutionResult, CheckAvailabilityResult>();
        AssertAssignableTo<ToolExecutionResult, RequestHumanHandoffResult>();
        AssertAssignableTo<ToolExecutionResult, CreateCalendarEventResult>();
    }

    private static void AssertAssignableTo<TBase, TDerived>()
    {
        typeof(TBase).IsAssignableFrom(typeof(TDerived)).ShouldBeTrue();
    }

    private static void AssertJsonProperty<TEntity, TProperty>(
        Microsoft.EntityFrameworkCore.Metadata.IModel model,
        string propertyName,
        string columnName)
    {
        var entityType = model.FindEntityType(typeof(TEntity));
        entityType.ShouldNotBeNull();

        var property = entityType.FindProperty(propertyName);
        property.ShouldNotBeNull();
        property.ClrType.ShouldBe(typeof(TProperty));
        property.GetColumnType().ShouldBe("jsonb");
        property.GetColumnName().ShouldBe(columnName);
    }

    private static class CEOAgentDbContextTestFactory
    {
        public static CEOAgentDbContext Create()
        {
            var options = new DbContextOptionsBuilder<CEOAgentDbContext>()
                .UseNpgsql("Host=localhost;Database=ceoagent_model_test;Username=postgres;Password=postgres")
                .UseSnakeCaseNamingConvention()
                .Options;

            return new CEOAgentDbContext(options, new CompanyContextAccessor(), TimeProvider.System);
        }
    }
}
