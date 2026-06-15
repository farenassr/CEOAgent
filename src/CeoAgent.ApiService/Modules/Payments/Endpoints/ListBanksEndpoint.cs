using CeoAgent.ApiService.Infrastructure.OpenApi;
using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Persistence.Extensions;
using CeoAgent.Shared.Response.Payment;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

namespace CeoAgent.ApiService.Modules.Payments.Endpoints;

public sealed class ListBanksEndpoint(CeoAgentDbContext dbContext) : EndpointWithoutRequest<BankListResponse>
{
    public override void Configure()
    {
        Get("/v1/admin/banks");
        Description(builder => builder
            .WithTags("Payments")
            .WithSummary("List Active Banks")
            .WithDescription("Lists active global bank catalog entries reusable by all companies."));
        Summary(summary =>
        {
            summary.Summary = "List Active Banks";
            summary.Description = "Lists active global bank catalog entries reusable by all companies.";
        });
    }

    public override async Task HandleAsync(CancellationToken cancellationToken)
    {
        var banks = await dbContext.Banks
            .AsNoTracking()
            .Active()
            .OrderedForCatalog()
            .Select(bank => PaymentMapper.ToResponse(bank))
            .ToListAsync(cancellationToken);

        await Send.OkAsync(new BankListResponse { Banks = banks }, cancellationToken);
    }
}
