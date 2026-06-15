using CeoAgent.Application.Abstractions.Organization;
using CeoAgent.Infrastructure.Implementation.Organization;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.IntegrationTests.Infrastructure;
using CeoAgent.Infrastructure.Implementation.AITools.GoogleCalendar;
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
            new OrganizationContextAccessor());
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
        typeof(CheckAvailabilityRequest).IsAssignableTo(typeof(ToolExecutionRequest)).ShouldBeFalse();
        typeof(CheckAvailabilityResult).IsAssignableTo(typeof(ToolExecutionResult)).ShouldBeFalse();
    }

    [Test]
    public void MessageModel_UsesMessageTextForCanonicalTextContent()
    {
        using var dbContext = CeoAgentDbContextTestFactory.CreatePostgres(
            "Host=localhost;Database=CeoAgent_model_test;Username=postgres;Password=postgres",
            new OrganizationContextAccessor());
        var entityType = dbContext.Model.FindEntityType(typeof(Message));
        entityType.ShouldNotBeNull();

        entityType.FindProperty(nameof(Message.MessageText)).ShouldNotBeNull();
        entityType.FindProperty("Text").ShouldBeNull();
    }

    [Test]
    public void CompanyToolModel_MapsGoogleCalendarSchedulingConfigProperties()
    {
        using var dbContext = CeoAgentDbContextTestFactory.CreatePostgres(
            "Host=localhost;Database=CeoAgent_model_test;Username=postgres;Password=postgres",
            new OrganizationContextAccessor());
        var entityType = dbContext.Model.FindEntityType(typeof(CompanyTool));
        entityType.ShouldNotBeNull();

        var configuration = entityType.FindComplexProperty(nameof(CompanyTool.Configuration));
        configuration.ShouldNotBeNull();
        var googleCalendar = configuration.ComplexType.FindComplexProperty(nameof(ToolConfiguration.GoogleCalendar));
        googleCalendar.ShouldNotBeNull();

        googleCalendar.ComplexType.FindProperty(nameof(GoogleCalendarConfig.ReservationMinutes)).ShouldNotBeNull();
        googleCalendar.ComplexType.FindProperty(nameof(GoogleCalendarConfig.AdvanceBookingDays)).ShouldNotBeNull();
        googleCalendar.ComplexType.FindProperty(nameof(GoogleCalendarConfig.SlotMinutes)).ShouldNotBeNull();
    }

    [Test]
    public void Model_DoesNotMapAudioAssetAsASeparateTable()
    {
        using var dbContext = CeoAgentDbContextTestFactory.CreatePostgres(
            "Host=localhost;Database=CeoAgent_model_test;Username=postgres;Password=postgres",
            new OrganizationContextAccessor());

        dbContext.Model.GetEntityTypes()
            .Any(entityType => entityType.ClrType.Name == "AudioAsset")
            .ShouldBeFalse();

        dbContext.Model.GetRelationalModel()
            .Tables
            .Any(table => table.Name == "audio_asset")
            .ShouldBeFalse();
    }

    [Test]
    public void PaymentModel_MapsBankCatalogAndCompanyPaymentAccounts()
    {
        using var dbContext = CeoAgentDbContextTestFactory.CreatePostgres(
            "Host=localhost;Database=CeoAgent_model_test;Username=postgres;Password=postgres",
            new OrganizationContextAccessor());
        var model = dbContext.GetService<IDesignTimeModel>().Model;

        var bankType = model.FindEntityType(typeof(Bank));
        bankType.ShouldNotBeNull();
        bankType.GetTableName().ShouldBe("bank");
        bankType.FindProperty(nameof(Bank.Name)).ShouldNotBeNull();
        bankType.FindProperty(nameof(Bank.CountryCode)).ShouldNotBeNull();
        bankType.GetDeclaredQueryFilters().ShouldBeEmpty("Bank is a global catalog and must not be organization-filtered.");

        var paymentAccountType = model.FindEntityType(typeof(CompanyPaymentAccount));
        paymentAccountType.ShouldNotBeNull();
        paymentAccountType.GetTableName().ShouldBe("company_payment_account");
        paymentAccountType.FindProperty(nameof(CompanyPaymentAccount.QrBlobContainer)).ShouldNotBeNull();
        paymentAccountType.FindProperty(nameof(CompanyPaymentAccount.QrBlobName)).ShouldNotBeNull();
        paymentAccountType.FindProperty(nameof(CompanyPaymentAccount.QrBlobUri)).ShouldNotBeNull();
        paymentAccountType.FindProperty("QrUrl").ShouldBeNull("Payment accounts must store private blob references, not public URLs.");
        paymentAccountType.GetDeclaredQueryFilters().ShouldNotBeEmpty("Company payment accounts must be organization-filtered.");

        var defaultIndex = paymentAccountType.GetIndexes().SingleOrDefault(index =>
            index.Properties.Select(property => property.Name).SequenceEqual([
                nameof(CompanyPaymentAccount.OrganizationId),
                nameof(CompanyPaymentAccount.Currency),
            ]));
        defaultIndex.ShouldNotBeNull();
        defaultIndex.IsUnique.ShouldBeTrue();
        defaultIndex.GetFilter().ShouldBe("is_default AND is_active");
    }

    [Test]
    public void Model_AddsCompanyCreatedAtDescendingIndexToAuditableOrganizationOwnedTables()
    {
        using var dbContext = CeoAgentDbContextTestFactory.CreatePostgres(
            "Host=localhost;Database=CeoAgent_model_test;Username=postgres;Password=postgres",
            new OrganizationContextAccessor());
        var model = dbContext.GetService<IDesignTimeModel>().Model;

        var auditableOrganizationOwnedTypes = typeof(AuditableOrganizationOwnedEntity).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && typeof(AuditableOrganizationOwnedEntity).IsAssignableFrom(type))
            .ToArray();

        foreach (var clrType in auditableOrganizationOwnedTypes)
        {
            var entityType = model.FindEntityType(clrType);
            entityType.ShouldNotBeNull();

            var index = entityType.GetIndexes()
                .SingleOrDefault(index => index.Properties.Select(property => property.Name).SequenceEqual([
                    nameof(AuditableOrganizationOwnedEntity.OrganizationId),
                    nameof(AuditableOrganizationOwnedEntity.CreatedAt),
                ]));

            index.ShouldNotBeNull($"{clrType.Name} should have an index on OrganizationId + CreatedAt.");
            index.IsDescending.ShouldNotBeNull($"{clrType.Name} should configure index sort order.");
            index.IsDescending.ShouldBe([false, true]);
        }
    }

    [Test]
    public void Model_AddsQueryFilterToEveryAuditableOrganizationOwnedEntity()
    {
        using var dbContext = CeoAgentDbContextTestFactory.CreatePostgres(
            "Host=localhost;Database=CeoAgent_model_test;Username=postgres;Password=postgres",
            new OrganizationContextAccessor());
        var model = dbContext.Model;

        var auditableOrganizationOwnedTypes = typeof(AuditableOrganizationOwnedEntity).Assembly
            .GetTypes()
            .Where(type => !type.IsAbstract && typeof(AuditableOrganizationOwnedEntity).IsAssignableFrom(type))
            .ToArray();

        foreach (var clrType in auditableOrganizationOwnedTypes)
        {
            var entityType = model.FindEntityType(clrType);
            entityType.ShouldNotBeNull();
            entityType.GetDeclaredQueryFilters().ShouldNotBeEmpty($"{clrType.Name} should have a global company query filter.");
        }
    }

    [Test]
    public async Task JsonbComplexWrappers_RoundTripPreviouslyPolymorphicDocuments()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var organizationId = Guid.CreateVersion7();
        database.OrganizationContext.SetOrganization(organizationId);

        var company = new CompanyEntity
        {
            Id = organizationId,
            Name = "Contoso Bistro",
            TimeZoneId = "America/Bogota",
        };
        var tool = new CompanyTool
        {
            OrganizationId = organizationId,
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
    public async Task CompanyWorkingHours_ReadsLowercaseTimeSlotJson()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var organizationId = Guid.CreateVersion7();
        database.OrganizationContext.SetOrganization(organizationId);
        const string workingHoursJson = """
            {
              "holidays": [],
              "schedule": {
                "monday": [{ "start": "08:00:00", "end": "17:00:00" }],
                "tuesday": [{ "start": "08:00:00", "end": "17:00:00" }],
                "wednesday": [{ "start": "08:00:00", "end": "17:00:00" }],
                "thursday": [{ "start": "08:00:00", "end": "17:00:00" }],
                "friday": [{ "start": "08:00:00", "end": "17:00:00" }],
                "saturday": [],
                "sunday": []
              }
            }
            """;

        await database.Context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO company (id, name, time_zone_id, status, created_at, updated_at, working_hours_json)
            VALUES ({organizationId}, 'Contoso Bistro', 'America/Bogota', 'Active', now(), now(), {workingHoursJson}::jsonb)
            """);
        database.Context.ChangeTracker.Clear();

        var loaded = await database.Context.Companies.SingleAsync(entity => entity.Id == organizationId);

        loaded.WorkingHours.ShouldNotBeNull();
        loaded.WorkingHours.Schedule.ShouldNotBeNull();
        loaded.WorkingHours.Schedule.Monday.ShouldNotBeNull();
        var mondaySlot = loaded.WorkingHours.Schedule.Monday.Single();
        mondaySlot.Start.ShouldBe(new TimeOnly(8, 0));
        mondaySlot.End.ShouldBe(new TimeOnly(17, 0));

        var start = GoogleCalendarSchedulingPolicy.ToCompanyLocalOffset(
            new DateOnly(2026, 6, 1),
            new TimeOnly(14, 30),
            loaded.TimeZoneId);
        var end = start.AddMinutes(GoogleCalendarSchedulingPolicy.DefaultReservationMinutes);
        GoogleCalendarSchedulingPolicy.IsWithinWorkingHours(loaded.WorkingHours, start, end).ShouldBeTrue();
    }

    [Test]
    public async Task IntegrationCredentialReference_StoresProviderUsingSnakeCaseEnumMemberName()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var organizationId = Guid.CreateVersion7();
        database.OrganizationContext.SetOrganization(organizationId);

        database.Context.Companies.Add(new CompanyEntity
        {
            Id = organizationId,
            Name = "Contoso Bistro",
            TimeZoneId = "America/Bogota",
        });
        database.Context.IntegrationCredentialReferences.Add(new IntegrationCredentialReference
        {
            OrganizationId = organizationId,
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

    [Test]
    public async Task IntegrationCredentialReference_RoundTripsGoogleCalendarReferenceOnlyMetadata()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var organizationId = Guid.CreateVersion7();
        database.OrganizationContext.SetOrganization(organizationId);

        database.Context.Companies.Add(new CompanyEntity
        {
            Id = organizationId,
            Name = "Contoso Bistro",
            TimeZoneId = "America/Bogota",
        });
        var credential = new IntegrationCredentialReference
        {
            OrganizationId = organizationId,
            Provider = IntegrationProvider.GoogleCalendar,
            Purpose = "google_calendar",
            Reference = "kv://google-calendar/contoso/service-account",
            Metadata = CredentialMetadata.ForGoogleCalendar(new GoogleCalendarCredentialMetadata
            {
                CalendarId = "primary",
                Scope = "calendar.events",
                ExpiresAt = new DateTimeOffset(2026, 6, 8, 12, 0, 0, TimeSpan.Zero),
            }),
        };
        database.Context.IntegrationCredentialReferences.Add(credential);
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();

        var loaded = await database.Context.IntegrationCredentialReferences.SingleAsync(entity => entity.Id == credential.Id);

        loaded.Metadata.ShouldNotBeNull();
        var metadata = loaded.Metadata.GoogleCalendar;
        metadata.ShouldNotBeNull();
        metadata.CalendarId.ShouldBe("primary");
        metadata.Scope.ShouldBe("calendar.events");
        metadata.ExpiresAt.ShouldBe(new DateTimeOffset(2026, 6, 8, 12, 0, 0, TimeSpan.Zero));
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
