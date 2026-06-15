using CeoAgent.Application.Abstractions.Storage;
using CeoAgent.ApiService.Infrastructure.Storage;
using CeoAgent.ApiService.Infrastructure.Security;
using CeoAgent.Infrastructure;
using CeoAgent.Shared.Request.Payment;
using CeoAgent.Shared.Response.Payment;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace CeoAgent.ApiService.Modules.Payments.Endpoints;

public sealed class UpdatePaymentAccountEndpoint(
    CeoAgentDbContext dbContext,
    IBlobStorageService blobStorage,
    IAdminTenantGuard tenantGuard) : Endpoint<CompanyPaymentAccountRequest, CompanyPaymentAccountResponse>
{
    public override void Configure()
    {
        Put("/v1/admin/payment-accounts/{paymentAccountId}");
        AllowFileUploads();
        Description(builder => builder
            .WithTags("Payments")
            .WithSummary("Update Company Payment Account")
            .WithDescription("Updates a payment account for the authenticated company."));
        Summary(summary =>
        {
            summary.Summary = "Update Company Payment Account";
            summary.Description = "Updates a payment account for the authenticated company.";
        });
    }

    public override async Task HandleAsync(CompanyPaymentAccountRequest request, CancellationToken cancellationToken)
    {
        var organizationId = tenantGuard.RequireAuthenticatedOrganizationId();
        await PaymentEndpointHelpers.EnsureActiveBankAsync(dbContext, request.BankId, cancellationToken);
        var paymentAccountId = Route<Guid>("paymentAccountId");
        var account = await PaymentEndpointHelpers.GetPaymentAccountAsync(
            dbContext,
            organizationId,
            paymentAccountId,
            trackChanges: true,
            cancellationToken);

        PaymentMapper.Apply(request, account);
        if (!account.IsActive)
        {
            account.IsDefault = false;
        }

        if (account.IsDefault && account.IsActive)
        {
            await PaymentEndpointHelpers.ClearOtherDefaultAccountsAsync(
                dbContext,
                organizationId,
                account.Currency,
                account.Id,
                cancellationToken);
        }

        var qrImage = FormFileBlobUpload.GetFile(Files, PaymentAccountQrFilePolicy.FormFieldName);
        if (qrImage is not null)
        {
            if (FormFileBlobUpload.ValidateRequired(qrImage, PaymentAccountQrFilePolicy.ValidationOptions) is { } qrImageError)
            {
                AddError(PaymentAccountQrFilePolicy.FormFieldName, qrImageError);
                await Send.ErrorsAsync(StatusCodes.Status400BadRequest, cancellationToken);
                return;
            }

            PaymentAccountQrFilePolicy.ApplyReference(account, qrImage.FileName);
            var upload = await FormFileBlobUpload.UploadAsync(
                blobStorage,
                qrImage,
                PaymentAccountQrFilePolicy.ReferenceFor(account),
                PaymentAccountQrFilePolicy.TagsFor(account),
                cancellationToken);
            account.QrBlobUri = upload.BlobUri;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await dbContext.Entry(account).Reference(entity => entity.Bank).LoadAsync(cancellationToken);

        await Send.OkAsync(PaymentMapper.ToResponse(account), cancellationToken);
    }
}
