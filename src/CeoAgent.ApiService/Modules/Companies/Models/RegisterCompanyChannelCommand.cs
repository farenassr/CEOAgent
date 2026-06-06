using System.Text.Json;
using CeoAgent.Shared.Enums;

namespace CeoAgent.ApiService.Modules.Companies.Models;

public sealed record RegisterCompanyChannelCommand(
    Guid CompanyId,
    CompanyChannelProvider Provider,
    string ProviderChannelId,
    JsonElement? Metadata,
    Guid? CredentialReferenceId);
