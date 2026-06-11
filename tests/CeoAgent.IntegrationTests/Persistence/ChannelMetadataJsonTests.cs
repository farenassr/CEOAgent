using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Shared.Enums;
using CeoAgent.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shouldly;
using CompanyEntity = CeoAgent.Infrastructure.Entities.Company;

namespace CeoAgent.IntegrationTests.Persistence;

public sealed class ChannelMetadataJsonTests
{
    [Test]
    public void Factory_CreatesChannelMetadataWrapperForWhatsAppCloud()
    {
        var organizationId = Guid.CreateVersion7();

        var whatsApp = CompanyChannel.ForWhatsAppCloud(
            organizationId,
            "123456789012345",
            new WhatsAppCloudMetadata
            {
                BusinessAccountId = "987654321",
                PhoneNumberId = "123456789012345",
                VerifiedName = "Contoso Bistro",
            });

        whatsApp.Provider.ShouldBe(CompanyChannelProvider.WhatsAppCloud);
        whatsApp.Metadata.WhatsAppCloud.ShouldNotBeNull();
        whatsApp.Metadata.Instagram.ShouldBeNull();
        whatsApp.Metadata.Telegram.ShouldBeNull();
    }

    [Test]
    public void FutureProviderFactories_ThrowNotSupportedException()
    {
        var organizationId = Guid.CreateVersion7();

        Should.Throw<NotSupportedException>(() => CompanyChannel.ForInstagram(
            organizationId,
            "17841400000000000",
            new InstagramMetadata
            {
                IgUserId = "17841400000000000",
                PageId = "1010101010",
            }));
        Should.Throw<NotSupportedException>(() => CompanyChannel.ForTelegram(
            organizationId,
            "contoso_bot",
            new TelegramMetadata
            {
                BotUsername = "contoso_bot",
                ChatId = 123456789,
            }));
    }

    [Test]
    public async Task Match_ReadsLoadedChannelPayloadFromDatabase()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var organizationId = Guid.CreateVersion7();
        database.OrganizationContext.SetOrganization(organizationId);
        await SeedCompanyAsync(database.Context, organizationId);

        var channel = CompanyChannel.ForWhatsAppCloud(
            organizationId,
            "123456789012345",
            new WhatsAppCloudMetadata
            {
                BusinessAccountId = "987654321",
                PhoneNumberId = "123456789012345",
                VerifiedName = "Contoso Bistro",
            });

        database.Context.CompanyChannels.Add(channel);
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();

        var loaded = await database.Context.CompanyChannels.SingleAsync(entity => entity.Id == channel.Id);
        var verifiedName = loaded.Match(whatsApp => whatsApp.VerifiedName);

        verifiedName.ShouldBe("Contoso Bistro");
    }

    [Test]
    public async Task JsonbWrapper_SupportsServerSideWhereAndExecuteUpdateOnNestedPayload()
    {
        await using var database = await PostgresTestDatabase.CreateAsync();
        var organizationId = Guid.CreateVersion7();
        database.OrganizationContext.SetOrganization(organizationId);
        await SeedCompanyAsync(database.Context, organizationId);

        var channel = CompanyChannel.ForWhatsAppCloud(
            organizationId,
            "123456789012345",
            new WhatsAppCloudMetadata
            {
                BusinessAccountId = "987654321",
                PhoneNumberId = "123456789012345",
                VerifiedName = "Contoso Bistro",
            });

        database.Context.CompanyChannels.Add(channel);
        await database.Context.SaveChangesAsync();

        var matchingChannelIds = await database.Context.CompanyChannels
            .Where(entity => entity.Metadata.WhatsAppCloud!.VerifiedName == "Contoso Bistro")
            .Select(entity => entity.Id)
            .ToListAsync();

        matchingChannelIds.ShouldContain(channel.Id);

        await database.Context.CompanyChannels
            .Where(entity => entity.Id == channel.Id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(
                    entity => entity.Metadata.WhatsAppCloud!.VerifiedName,
                    "Contoso Bistro Verified"));

        database.Context.ChangeTracker.Clear();

        var updatedVerifiedName = await database.Context.CompanyChannels
            .Where(entity => entity.Id == channel.Id)
            .Select(entity => entity.Metadata.WhatsAppCloud!.VerifiedName)
            .SingleAsync();

        updatedVerifiedName.ShouldBe("Contoso Bistro Verified");
    }

    private static async Task SeedCompanyAsync(CeoAgentDbContext dbContext, Guid organizationId)
    {
        dbContext.Companies.Add(new CompanyEntity
        {
            Id = organizationId,
            Name = "Contoso Bistro",
            TimeZoneId = "America/Bogota",
        });

        await dbContext.SaveChangesAsync();
    }
}
