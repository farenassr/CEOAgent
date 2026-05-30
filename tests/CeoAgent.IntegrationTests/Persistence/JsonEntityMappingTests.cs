using CeoAgent.Application.Company;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.IntegrationTests.Infrastructure;
using CeoAgent.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
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

        AssertJsonComplexProperty<CompanyEntity, WorkingHoursEntity>(model, nameof(CompanyEntity.WorkingHours), "working_hours_json");
        AssertJsonComplexProperty<CompanyChannel, ChannelMetadata>(model, nameof(CompanyChannel.Metadata), "metadata_json");
        AssertJsonComplexProperty<CompanyTool, ToolConfiguration>(model, nameof(CompanyTool.Configuration), "configuration_json");
        AssertJsonComplexProperty<ConversationState, ConversationStateSnapshot>(model, nameof(ConversationState.Snapshot), "state_json");
        AssertJsonComplexProperty<IntegrationCredentialReference, CredentialMetadata>(model, nameof(IntegrationCredentialReference.Metadata), "metadata_json");
        AssertJsonComplexProperty<Message, MessagePayload>(model, nameof(Message.Payload), "payload_json");
        AssertJsonComplexProperty<ToolExecution, ToolExecutionRequest>(model, nameof(ToolExecution.Request), "request_json");
        AssertJsonComplexProperty<ToolExecution, ToolExecutionResult>(model, nameof(ToolExecution.Result), "result_json");
    }

    /// <summary>
    /// Verifies that JSON documents use concrete wrapper types instead of inheritance-based polymorphism.
    /// </summary>
    [Test]
    public void JsonEntityTypes_UseConcreteComplexPropertyWrappers()
    {
        typeof(ChannelMetadata).IsAssignableFrom(typeof(WhatsAppCloudMetadata)).ShouldBeFalse();
        typeof(ChannelMetadata).IsAssignableFrom(typeof(InstagramMetadata)).ShouldBeFalse();
        typeof(ChannelMetadata).IsAssignableFrom(typeof(TelegramMetadata)).ShouldBeFalse();

        typeof(ToolConfiguration).IsAbstract.ShouldBeFalse();
        typeof(CredentialMetadata).IsAbstract.ShouldBeFalse();
        typeof(MessagePayload).IsAbstract.ShouldBeFalse();
        typeof(ToolExecutionRequest).IsAbstract.ShouldBeFalse();
        typeof(ToolExecutionResult).IsAbstract.ShouldBeFalse();

        typeof(CheckAvailabilityConfig).IsAssignableTo(typeof(ToolConfiguration)).ShouldBeFalse();
        typeof(GoogleCalendarCredentialMetadata).IsAssignableTo(typeof(CredentialMetadata)).ShouldBeFalse();
        typeof(AudioPayload).IsAssignableTo(typeof(BlobPayload)).ShouldBeTrue();
        typeof(BlobPayload).IsAbstract.ShouldBeTrue();
        typeof(CheckAvailabilityRequest).IsAssignableTo(typeof(ToolExecutionRequest)).ShouldBeFalse();
        typeof(CheckAvailabilityResult).IsAssignableTo(typeof(ToolExecutionResult)).ShouldBeFalse();
    }

    [Test]
    public void MessageModel_UsesMessageTextForCanonicalTextContent()
    {
        using var dbContext = CeoAgentDbContextTestFactory.CreatePostgres(
            "Host=localhost;Database=CeoAgent_model_test;Username=postgres;Password=postgres",
            new CompanyContextAccessor());
        var entityType = dbContext.Model.FindEntityType(typeof(Message));
        entityType.ShouldNotBeNull();

        entityType.FindProperty(nameof(Message.MessageText)).ShouldNotBeNull();
        entityType.FindProperty("Text").ShouldBeNull();
    }

    [Test]
    public void Model_DoesNotMapAudioAssetAsASeparateTable()
    {
        using var dbContext = CeoAgentDbContextTestFactory.CreatePostgres(
            "Host=localhost;Database=CeoAgent_model_test;Username=postgres;Password=postgres",
            new CompanyContextAccessor());

        dbContext.Model.GetEntityTypes()
            .Any(entityType => entityType.ClrType.Name == "AudioAsset")
            .ShouldBeFalse();

        dbContext.Model.GetRelationalModel()
            .Tables
            .Any(table => table.Name == "audio_asset")
            .ShouldBeFalse();
    }

    [Test]
    public void Model_AddsCompanyCreatedAtDescendingIndexToAuditableCompanyOwnedTables()
    {
        using var dbContext = CeoAgentDbContextTestFactory.CreatePostgres(
            "Host=localhost;Database=CeoAgent_model_test;Username=postgres;Password=postgres",
            new CompanyContextAccessor());
        var model = dbContext.GetService<IDesignTimeModel>().Model;

        var auditableCompanyOwnedTypes = typeof(AuditableCompanyOwnedEntity).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && typeof(AuditableCompanyOwnedEntity).IsAssignableFrom(type))
            .ToArray();

        foreach (var clrType in auditableCompanyOwnedTypes)
        {
            var entityType = model.FindEntityType(clrType);
            entityType.ShouldNotBeNull();

            var index = entityType.GetIndexes()
                .SingleOrDefault(index => index.Properties.Select(property => property.Name).SequenceEqual([
                    nameof(AuditableCompanyOwnedEntity.CompanyId),
                    nameof(AuditableCompanyOwnedEntity.CreatedAt),
                ]));

            index.ShouldNotBeNull($"{clrType.Name} should have an index on CompanyId + CreatedAt.");
            index.IsDescending.ShouldNotBeNull($"{clrType.Name} should configure index sort order.");
            index.IsDescending.ShouldBe([false, true]);
        }
    }

    [Test]
    public void MessagePayload_UsesAudioComplexTypeWithSharedBlobFieldsAndEnumStatuses()
    {
        typeof(BlobPayload).GetProperty(nameof(BlobPayload.BlobUri))!.PropertyType.ShouldBe(typeof(string));
        typeof(BlobPayload).GetProperty(nameof(BlobPayload.ContentType))!.PropertyType.ShouldBe(typeof(string));
        typeof(BlobPayload).GetProperty(nameof(BlobPayload.SizeBytes))!.PropertyType.ShouldBe(typeof(long));
        typeof(AudioPayload).GetProperty(nameof(AudioPayload.Language))!.PropertyType.ShouldBe(typeof(string));
        typeof(AudioPayload).GetProperty(nameof(AudioPayload.DurationMs))!.PropertyType.ShouldBe(typeof(int?));
        typeof(AudioPayload).GetProperty(nameof(AudioPayload.SttStatus))!.PropertyType.ShouldBe(typeof(SpeechProcessingStatus?));
        typeof(AudioPayload).GetProperty(nameof(AudioPayload.TtsStatus))!.PropertyType.ShouldBe(typeof(SpeechProcessingStatus?));
    }

    [Test]
    public async Task JsonbComplexWrappers_RoundTripPreviouslyPolymorphicDocuments()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var companyId = Guid.CreateVersion7();
        database.CompanyContext.SetCompany(companyId);

        var company = new CompanyEntity
        {
            Id = companyId,
            Name = "Contoso Bistro",
            TimeZoneId = "America/Bogota",
        };
        var tool = new CompanyTool
        {
            CompanyId = companyId,
            ToolKey = "check_availability",
            Configuration = ToolConfiguration.ForCheckAvailability(new CheckAvailabilityConfig
            {
                MinPartySize = 1,
                MaxPartySize = 8,
                SlotMinutes = 30,
                AdvanceBookingDays = 14,
            }),
        };

        database.Context.Companies.Add(company);
        database.Context.CompanyTools.Add(tool);
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();

        var loaded = await database.Context.CompanyTools.SingleAsync(entity => entity.Id == tool.Id);

        loaded.Configuration.ShouldNotBeNull();
        loaded.Configuration.ToolKey.ShouldBe("check_availability");
        loaded.Configuration.CheckAvailability.ShouldNotBeNull();
        loaded.Configuration.CheckAvailability.MaxPartySize.ShouldBe(8);
    }

    [Test]
    public async Task IntegrationCredentialReference_StoresProviderUsingSnakeCaseEnumMemberName()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var companyId = Guid.CreateVersion7();
        database.CompanyContext.SetCompany(companyId);

        database.Context.Companies.Add(new CompanyEntity
        {
            Id = companyId,
            Name = "Contoso Bistro",
            TimeZoneId = "America/Bogota",
        });
        database.Context.IntegrationCredentialReferences.Add(new IntegrationCredentialReference
        {
            CompanyId = companyId,
            Provider = IntegrationProvider.WhatsAppCloud,
            Purpose = "message_send",
            Reference = "kv://whatsapp/contoso/access-token",
        });

        await database.Context.SaveChangesAsync();

        var provider = await database.Context.Database
            .SqlQueryRaw<string>("SELECT provider AS \"Value\" FROM integration_credential_reference")
            .SingleAsync();

        provider.ShouldBe("whatsapp_cloud");
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
