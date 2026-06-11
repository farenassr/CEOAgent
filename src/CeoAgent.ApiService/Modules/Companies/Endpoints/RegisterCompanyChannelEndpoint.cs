using CeoAgent.ApiService.Modules.Companies.Mappers;
using CeoAgent.Shared.Request.Company;
using CeoAgent.Shared.Response.Company;
using FastEndpoints;
using FluentValidation;
using Mediator;

namespace CeoAgent.ApiService.Modules.Companies.Endpoints;

/// <summary>
/// Registers a provider channel for company resolution.
/// </summary>
public sealed class RegisterCompanyChannelEndpoint(
    ISender sender) : Endpoint<CompanyChannelRequest, CompanyChannelResponse>
{
    public override void Configure()
    {
        Post("/v1/admin/companies/{organizationId}/channels");
    }

    public override async Task HandleAsync(CompanyChannelRequest request, CancellationToken cancellationToken)
    {
        var organizationId = Route<Guid>("organizationId");
        var channel = await sender.Send(
            CompanyMapper.ToCommand(request, organizationId),
            cancellationToken);

        await Send.OkAsync(CompanyMapper.ToResponse(channel), cancellationToken);
    }
}

public sealed class CompanyChannelValidator : Validator<CompanyChannelRequest>
{
    public CompanyChannelValidator()
    {
        RuleFor(request => request.Provider).IsInEnum();
        RuleFor(request => request.ProviderChannelId).NotEmpty().MaximumLength(160);
    }
}
