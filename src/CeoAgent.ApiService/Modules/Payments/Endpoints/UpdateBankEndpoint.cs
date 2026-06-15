using CeoAgent.ApiService.Infrastructure.OpenApi;
using CeoAgent.Application.Errors;
using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Persistence.Extensions;
using CeoAgent.Shared.Request.Payment;
using CeoAgent.Shared.Response.Payment;
using FastEndpoints;
using Microsoft.EntityFrameworkCore;

namespace CeoAgent.ApiService.Modules.Payments.Endpoints;

public sealed class UpdateBankEndpoint(CeoAgentDbContext dbContext) : Endpoint<BankRequest, BankResponse>
{
    public override void Configure()
    {
        Put("/v1/admin/banks/{bankId}");
        Description(builder => builder
            .WithTags("Payments")
            .WithSummary("Update Bank")
            .WithDescription("Updates a global bank catalog entry."));
        Summary(summary =>
        {
            summary.Summary = "Update Bank";
            summary.Description = "Updates a global bank catalog entry.";
        });
    }

    public override async Task HandleAsync(BankRequest request, CancellationToken cancellationToken)
    {
        var bankId = Route<Guid>("bankId");
        var bank = await dbContext.Banks
            .WithId(bankId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new NotFoundException("bank", bankId);

        PaymentMapper.Apply(request, bank);
        await dbContext.SaveChangesAsync(cancellationToken);

        await Send.OkAsync(PaymentMapper.ToResponse(bank), cancellationToken);
    }
}
