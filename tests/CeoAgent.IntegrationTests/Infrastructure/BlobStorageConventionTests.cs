using Azure.Storage.Blobs;
using CeoAgent.Application.Abstractions.Payments;
using CeoAgent.Application.Abstractions.Storage;
using CeoAgent.Infrastructure.DependencyInjection;
using CeoAgent.Infrastructure.Implementation.Messaging.Payments;
using CeoAgent.Infrastructure.Implementation.Messaging.Storage;
using CeoAgent.Infrastructure.Persistence;
using CeoAgent.Shared.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

namespace CeoAgent.IntegrationTests.Infrastructure;

public sealed class BlobStorageConventionTests
{
    [Test]
    public void PaymentQrReference_UsesPrivateContainerAndFileNameGuidBlobName()
    {
        var paymentAccountId = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b35");

        var reference = BlobStorageNaming.ForPaymentQr("Banco QR Final.PNG", paymentAccountId);

        reference.ContainerName.ShouldBe(BlobStorageContainerNames.Private);
        reference.BlobName.ShouldBe($"banco-qr-final-{paymentAccountId:D}.png");
    }

    [Test]
    public void ConversationMediaReference_UsesPrivateContainerAndConversationMessageAssetPath()
    {
        var organizationId = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30");
        var conversationId = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b34");
        var messageId = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b36");
        var assetId = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b37");

        var reference = BlobStorageNaming.ForConversationMedia(
            "Contoso Bistro",
            organizationId,
            conversationId,
            messageId,
            assetId,
            ".jpg");

        reference.ContainerName.ShouldBe(BlobStorageContainerNames.Private);
        reference.BlobName.ShouldBe(
            $"organizations/contoso-bistro-{organizationId:D}/conversations/{conversationId:D}/messages/{messageId:D}/media/{assetId:D}.jpg");
    }

    [Test]
    public void PublicAssetReference_UsesPublicContainerAndOrganizationScopedAssetPath()
    {
        var organizationId = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30");
        var assetId = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b38");

        var reference = BlobStorageNaming.ForPublicAsset("Nandu Cafe", organizationId, assetId);

        reference.ContainerName.ShouldBe(BlobStorageContainerNames.Public);
        reference.BlobName.ShouldBe($"organizations/nandu-cafe-{organizationId:D}/assets/{assetId:D}");
    }

    [Test]
    public void ValidateReference_RejectsContainersOutsidePrivateAndPublic()
    {
        var action = () => BlobStorageReference.Create("payment-qr", "organizations/demo/file.png");

        action.ShouldThrow<ArgumentException>()
            .Message.ShouldContain("private or public");
    }

    [Test]
    public void PaymentQrTags_UseAllowedNonSensitiveOperationalTags()
    {
        var organizationId = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30");
        var paymentAccountId = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b35");

        var tags = BlobStorageTags.ForPaymentQr(organizationId, paymentAccountId);

        tags.ShouldContainKeyAndValue("organization_id", organizationId.ToString("D"));
        tags.ShouldContainKeyAndValue("visibility", "private");
        tags.ShouldContainKeyAndValue("category", "payment_qr");
        tags.ShouldContainKeyAndValue("status", "active");
        tags.ShouldContainKeyAndValue("content_kind", "image");
        tags.ShouldContainKeyAndValue("payment_account_id", paymentAccountId.ToString("D"));
        tags.ShouldContainKeyAndValue("retention", "permanent");
        tags.Keys.ShouldNotContain("customer_phone");
        tags.Keys.ShouldNotContain("bank_account_number");
    }

    [Test]
    public void ValidateTags_RejectsUnknownTagNames()
    {
        var tags = new Dictionary<string, string>
        {
            ["organization_id"] = Guid.CreateVersion7().ToString("D"),
            ["visibility"] = "private",
            ["category"] = "payment_qr",
            ["status"] = "active",
            ["content_kind"] = "image",
            ["bank_account_number"] = "0011223344",
        };

        var action = () => BlobStorageTags.Validate(tags);

        action.ShouldThrow<ArgumentException>()
            .Message.ShouldContain("bank_account_number");
    }

    [Test]
    public void AddInfrastructure_RegistersCentralBlobStorageAndQrProviderWhenBlobClientExists()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Persistence:UseInMemoryDatabase"] = "true",
                ["Persistence:InMemoryDatabaseName"] = "blob-storage-registration-test-db",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton(Substitute.For<BlobServiceClient>());

        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<PersistenceOptions>().UseInMemoryDatabase.ShouldBeTrue();
        provider.GetRequiredService<IBlobStorageService>().ShouldBeOfType<AzureBlobStorageService>();
        provider.GetRequiredService<IPaymentQrImageProvider>().ShouldBeOfType<BlobPaymentQrImageProvider>();
    }
}
