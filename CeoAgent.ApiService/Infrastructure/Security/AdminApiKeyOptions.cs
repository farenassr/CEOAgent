using System;

namespace CeoAgent.ApiService.Infrastructure.Security;

public sealed class AdminApiKeyOptions
{
    public string Key { get; set; } = default!;
    public Guid CompanyId { get; set; }
}
