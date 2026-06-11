namespace CeoAgent.Shared.Messaging;

public sealed record ChannelMessageReference(
    Guid OrganizationId,
    Guid CompanyChannelId,
    string Provider,
    string ProviderMessageId);
