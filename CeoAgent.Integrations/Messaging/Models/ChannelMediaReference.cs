namespace CeoAgent.Integrations.Messaging;

public sealed record ChannelMediaReference(
    Guid CompanyId,
    Guid CompanyChannelId,
    string Provider,
    string ProviderMediaId);
