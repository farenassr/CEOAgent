using CeoAgent.Shared.Enums;
using CeoAgent.Shared.Media;
using Shouldly;

namespace CeoAgent.IntegrationTests.Media;

public sealed class AudioBlobNamingTests
{
    [Test]
    public void CreatePath_UsesCompanyScopedAudioLayout()
    {
        var companyId = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30");
        var messageId = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b36");
        var createdAtUtc = new DateTimeOffset(2026, 5, 28, 12, 15, 0, TimeSpan.Zero);

        var path = AudioBlobNaming.CreatePath(
            "Contoso Bistro!",
            companyId,
            AudioBlobDirection.Inbound,
            createdAtUtc,
            messageId,
            ".ogg");

        path.ShouldBe("companies/contoso-bistro-018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30/media/audio/inbound/2026-05-28/018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b36.ogg");
    }

    [Test]
    public void CreateMetadata_OnlyIncludesAllowedAudioMetadata()
    {
        var metadata = AudioBlobNaming.CreateMetadata(new AudioBlobMetadataRequest
        {
            CompanyId = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b30"),
            CompanySlug = "contoso-bistro",
            ConversationId = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b34"),
            MessageId = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b36"),
            CustomerId = Guid.Parse("018f4f70-8b5f-7b4c-9d1a-0f6c1d7a2b33"),
            Direction = AudioBlobDirection.Inbound,
            Provider = "whatsapp_cloud",
            ProviderMediaId = "wamedia-123",
            ContentType = "audio/ogg",
            OriginalExtension = ".ogg",
            CreatedAtUtc = new DateTimeOffset(2026, 5, 28, 12, 15, 0, TimeSpan.Zero),
        });

        metadata.Keys.Order().ShouldBe([
            "company_id",
            "company_slug",
            "content_type",
            "conversation_id",
            "created_at_utc",
            "customer_id",
            "direction",
            "message_id",
            "original_extension",
            "provider",
            "provider_media_id",
        ]);
        metadata.ShouldNotContainKey("phone_number");
        metadata.ShouldNotContainKey("transcription_text");
        metadata.ShouldNotContainKey("prompt");
        metadata.ShouldNotContainKey("assistant_response");
        metadata["direction"].ShouldBe("inbound");
    }
}
