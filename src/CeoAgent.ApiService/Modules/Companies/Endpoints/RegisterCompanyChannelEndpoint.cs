using CeoAgent.ApiService.Infrastructure.OpenApi;
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
        Description(builder => builder
            .WithTags(OpenApiConstants.Tags.Channels)
            .WithSummary("Register Company Channel")
            .WithDescription("Registers an external provider channel for a company. Use it to map provider channel identifiers, such as WhatsApp Cloud IDs, to the company that should receive those messages."));
        Summary(summary =>
        {
            summary.Summary = "Register Company Channel";
            summary.Description = "Registers an external provider channel for a company. Use it to map provider channel identifiers, such as WhatsApp Cloud IDs, to the company that should receive those messages.";
        });
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
