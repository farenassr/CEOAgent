using CEOAgent.ApiService.Modules.Companies.Mappers;
using CEOAgent.Shared.Request.Company;
using CEOAgent.Shared.Response.Company;
using FastEndpoints;
using FluentValidation;
using Mediator;

namespace CEOAgent.ApiService.Modules.Companies.Endpoints;

/// <summary>
/// Registers a provider channel for company resolution.
/// </summary>
public sealed class RegisterCompanyChannelEndpoint(
    ISender sender) : Endpoint<CompanyChannelRequest, CompanyChannelResponse>
{
    public override void Configure()
    {
        Post("/v1/admin/companies/{companyId}/channels");
    }

    public override async Task HandleAsync(CompanyChannelRequest request, CancellationToken cancellationToken)
    {
        var companyId = Route<Guid>("companyId");
        var channel = await sender.Send(
            CompanyMapper.ToCommand(request, companyId),
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
