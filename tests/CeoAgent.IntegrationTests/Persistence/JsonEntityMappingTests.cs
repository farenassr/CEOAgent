using CeoAgent.Application.Company;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using CompanyEntity = CeoAgent.Infrastructure.Entities.Company;
using WorkingHoursEntity = CeoAgent.Infrastructure.Entities.JsonDocuments.WorkingHours;

namespace CeoAgent.IntegrationTests.Persistence;

public sealed class JsonEntityMappingTests
{
    /// <summary>
    /// Verifies that JSON-backed entity properties are mapped to stable jsonb column names.
    /// </summary>
    [Test]
    public void Model_MapsJsonbEntityProperties_AsTypedObjectsWithStableColumnNames()
    {
        using var dbContext = CeoAgentDbContextTestFactory.CreatePostgres(
            "Host=localhost;Database=CeoAgent_model_test;Username=postgres;Password=postgres",
            new CompanyContextAccessor());
        var model = dbContext.Model;

        AssertJsonProperty<CompanyEntity, WorkingHoursEntity>(model, nameof(CompanyEntity.WorkingHours), "working_hours_json");
        AssertJsonComplexProperty<CompanyChannel, ChannelMetadata>(model, nameof(CompanyChannel.Metadata), "metadata_json");
        AssertJsonProperty<CompanyTool, ToolConfiguration>(model, nameof(CompanyTool.Configuration), "configuration_json");
        AssertJsonProperty<ConversationState, ConversationStateSnapshot>(model, nameof(ConversationState.Snapshot), "state_json");
        AssertJsonProperty<IntegrationCredentialReference, CredentialMetadata>(model, nameof(IntegrationCredentialReference.Metadata), "metadata_json");
        AssertJsonProperty<Message, MessagePayload>(model, nameof(Message.Payload), "payload_json");
        AssertJsonProperty<ToolExecution, ToolExecutionRequest>(model, nameof(ToolExecution.Request), "request_json");
        AssertJsonProperty<ToolExecution, ToolExecutionResult>(model, nameof(ToolExecution.Result), "result_json");
    }

    /// <summary>
    /// Verifies that JSON entity base types expose the expected polymorphic derived types, except channels which use wrapper metadata.
    /// </summary>
    [Test]
    public void JsonEntityTypes_ExposeExpectedPolymorphicDerivedTypes()
    {
        typeof(ChannelMetadata).IsAssignableFrom(typeof(WhatsAppCloudMetadata)).ShouldBeFalse();
        typeof(ChannelMetadata).IsAssignableFrom(typeof(InstagramMetadata)).ShouldBeFalse();
        typeof(ChannelMetadata).IsAssignableFrom(typeof(TelegramMetadata)).ShouldBeFalse();

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

    private static void AssertJsonComplexProperty<TEntity, TProperty>(
        Microsoft.EntityFrameworkCore.Metadata.IModel model,
        string propertyName,
        string columnName)
    {
        var entityType = model.FindEntityType(typeof(TEntity));
        entityType.ShouldNotBeNull();

        var property = entityType.FindComplexProperty(propertyName);
        property.ShouldNotBeNull();
        property.ClrType.ShouldBe(typeof(TProperty));
        property.ComplexType.GetContainerColumnName().ShouldBe(columnName);

        var table = model.GetRelationalModel()
            .Tables
            .Single(table => table.Name == entityType.GetTableName() && table.Schema == entityType.GetSchema());
        table.Columns.Single(column => column.Name == columnName).StoreType.ShouldBe("jsonb");
    }
}
