using CeoAgent.Application.Abstractions.Storage;
using CeoAgent.ApiService.Infrastructure.Storage;
using CeoAgent.ApiService.Infrastructure.Security;
using CeoAgent.Infrastructure;
using CeoAgent.Shared.Request.Payment;
using CeoAgent.Shared.Response.Payment;
using FastEndpoints;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace CeoAgent.ApiService.Modules.Payments.Endpoints;

public sealed class CreatePaymentAccountEndpoint(
    CeoAgentDbContext dbContext,
    IBlobStorageService blobStorage,
    IAdminTenantGuard tenantGuard) : Endpoint<CompanyPaymentAccountRequest, CompanyPaymentAccountResponse>
{
    public override void Configure()
    {
        Post("/v1/admin/payment-accounts");
        AllowFileUploads();
        Description(builder => builder
            .WithTags("Payments")
            .WithSummary("Create Company Payment Account")
            .WithDescription("Creates a bank payment account for the authenticated company."));
        Summary(summary =>
        {
            summary.Summary = "Create Company Payment Account";
            summary.Description = "Creates a bank payment account for the authenticated company.";
        });
    }

    public override async Task HandleAsync(CompanyPaymentAccountRequest request, CancellationToken cancellationToken)
    {
        var qrImage = FormFileBlobUpload.GetFile(Files, PaymentAccountQrFilePolicy.FormFieldName);
        if (FormFileBlobUpload.ValidateRequired(qrImage, PaymentAccountQrFilePolicy.ValidationOptions) is { } qrImageError)
        {
            AddError(PaymentAccountQrFilePolicy.FormFieldName, qrImageError);
            await Send.ErrorsAsync(StatusCodes.Status400BadRequest, cancellationToken);
            return;
        }

        var organizationId = tenantGuard.RequireAuthenticatedOrganizationId();
        var company = await tenantGuard.GetAuthenticatedCompanyAsync(trackChanges: false, cancellationToken);
        await PaymentEndpointHelpers.EnsureActiveBankAsync(dbContext, request.BankId, cancellationToken);

        var account = PaymentMapper.ToPaymentAccount(request, organizationId, company.Name);
        PaymentAccountQrFilePolicy.ApplyReference(account, qrImage!.FileName);
        if (account.IsDefault && account.IsActive)
        {
            await PaymentEndpointHelpers.ClearOtherDefaultAccountsAsync(
                dbContext,
                organizationId,
                account.Currency,
                exceptAccountId: null,
                cancellationToken);
        }

        var upload = await FormFileBlobUpload.UploadAsync(
            blobStorage,
            qrImage,
            PaymentAccountQrFilePolicy.ReferenceFor(account),
            PaymentAccountQrFilePolicy.TagsFor(account),
            cancellationToken);
        account.QrBlobUri = upload.BlobUri;

        dbContext.CompanyPaymentAccounts.Add(account);
        await dbContext.SaveChangesAsync(cancellationToken);
        await dbContext.Entry(account).Reference(entity => entity.Bank).LoadAsync(cancellationToken);

        await Send.OkAsync(PaymentMapper.ToResponse(account), cancellationToken);
    }
}

public sealed class CompanyPaymentAccountValidator : Validator<CompanyPaymentAccountRequest>
{
    public CompanyPaymentAccountValidator()
    {
        RuleFor(request => request.BankId).NotEmpty();
        RuleFor(request => request.AccountNumber).NotEmpty().MaximumLength(80);
        RuleFor(request => request.AccountType).IsInEnum();
        RuleFor(request => request.AccountHolderName).MaximumLength(200);
        RuleFor(request => request.Currency).NotEmpty().Length(3);
        RuleFor(request => request.ReservationPaymentAmount).GreaterThan(0);
        RuleFor(request => request.IsActive)
            .Equal(true)
            .When(request => request.IsDefault)
            .WithMessage("A default payment account must be active.");
    }
}
