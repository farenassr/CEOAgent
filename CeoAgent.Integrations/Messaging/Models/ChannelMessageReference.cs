namespace CeoAgent.Integrations.Messaging;

public sealed record ChannelMessageReference(
    Guid CompanyId,
    Guid CompanyChannelId,
    string Provider,
    string ProviderMessageId);
