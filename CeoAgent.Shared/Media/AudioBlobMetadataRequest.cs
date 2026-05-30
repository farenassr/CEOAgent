using CeoAgent.Shared.Enums;

namespace CeoAgent.Shared.Media;

public sealed class AudioBlobMetadataRequest
{
    public required Guid CompanyId { get; init; }

    public required string CompanySlug { get; init; }

    public required Guid ConversationId { get; init; }

    public required Guid MessageId { get; init; }

    public required Guid CustomerId { get; init; }

    public required AudioBlobDirection Direction { get; init; }

    public required string Provider { get; init; }

    public string? ProviderMediaId { get; init; }

    public required string ContentType { get; init; }

    public required string OriginalExtension { get; init; }

    public required DateTimeOffset CreatedAtUtc { get; init; }
}
