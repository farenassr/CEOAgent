using CeoAgent.ApiService.Infrastructure.OpenApi;
using CeoAgent.Infrastructure;
using CeoAgent.Shared.Request.Payment;
using CeoAgent.Shared.Response.Payment;
using FastEndpoints;
using FluentValidation;

namespace CeoAgent.ApiService.Modules.Payments.Endpoints;

public sealed class CreateBankEndpoint(CeoAgentDbContext dbContext) : Endpoint<BankRequest, BankResponse>
{
    public override void Configure()
    {
        Post("/v1/admin/banks");
        Description(builder => builder
            .WithTags("Payments")
            .WithSummary("Create Bank")
            .WithDescription("Creates a global bank catalog entry for company payment account configuration."));
        Summary(summary =>
        {
            summary.Summary = "Create Bank";
            summary.Description = "Creates a global bank catalog entry for company payment account configuration.";
        });
    }

    public override async Task HandleAsync(BankRequest request, CancellationToken cancellationToken)
    {
        var bank = PaymentMapper.ToBank(request);
        dbContext.Banks.Add(bank);
        await dbContext.SaveChangesAsync(cancellationToken);

        await Send.OkAsync(PaymentMapper.ToResponse(bank), cancellationToken);
    }
}

public sealed class BankValidator : Validator<BankRequest>
{
    public BankValidator()
    {
        RuleFor(request => request.Name).NotEmpty().MaximumLength(200);
        RuleFor(request => request.CountryCode).NotEmpty().Length(2);
    }
}
