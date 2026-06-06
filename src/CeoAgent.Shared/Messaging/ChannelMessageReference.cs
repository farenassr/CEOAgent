namespace CeoAgent.Shared.Messaging;

public sealed record ChannelMessageReference(
    Guid CompanyId,
    Guid CompanyChannelId,
    string Provider,
    string ProviderMessageId);
