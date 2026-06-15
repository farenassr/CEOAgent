using CeoAgent.Infrastructure.Entities;
using CeoAgent.Shared.Request.Payment;
using CeoAgent.Shared.Response.Payment;
using CeoAgent.Shared.Storage;

namespace CeoAgent.ApiService.Modules.Payments;

internal static class PaymentMapper
{
    public static Bank ToBank(BankRequest request)
    {
        return new Bank
        {
            Name = NormalizeName(request.Name),
            CountryCode = NormalizeCountryCode(request.CountryCode),
            IsActive = request.IsActive,
        };
    }

    public static void Apply(BankRequest request, Bank bank)
    {
        bank.Name = NormalizeName(request.Name);
        bank.CountryCode = NormalizeCountryCode(request.CountryCode);
        bank.IsActive = request.IsActive;
    }

    public static BankResponse ToResponse(Bank bank)
    {
        return new BankResponse
        {
            Id = bank.Id,
            Name = bank.Name,
            CountryCode = bank.CountryCode,
            IsActive = bank.IsActive,
            CreatedAt = bank.CreatedAt,
            UpdatedAt = bank.UpdatedAt,
        };
    }

    public static CompanyPaymentAccount ToPaymentAccount(
        CompanyPaymentAccountRequest request,
        Guid organizationId,
        string organizationName)
    {
        var account = new CompanyPaymentAccount
        {
            OrganizationId = organizationId,
            BankId = request.BankId,
            AccountNumber = request.AccountNumber.Trim(),
            AccountType = request.AccountType,
            AccountHolderName = string.IsNullOrWhiteSpace(request.AccountHolderName) ? null : request.AccountHolderName.Trim(),
            Currency = NormalizeCurrency(request.Currency),
            ReservationPaymentAmount = request.ReservationPaymentAmount,
            QrBlobContainer = BlobStorageContainerNames.Private,
            QrBlobName = string.Empty,
            QrBlobUri = null,
            IsDefault = request.IsDefault,
            IsActive = request.IsActive,
        };
        return account;
    }

    public static void Apply(CompanyPaymentAccountRequest request, CompanyPaymentAccount account)
    {
        account.BankId = request.BankId;
        account.AccountNumber = request.AccountNumber.Trim();
        account.AccountType = request.AccountType;
        account.AccountHolderName = string.IsNullOrWhiteSpace(request.AccountHolderName) ? null : request.AccountHolderName.Trim();
        account.Currency = NormalizeCurrency(request.Currency);
        account.ReservationPaymentAmount = request.ReservationPaymentAmount;
        account.IsDefault = request.IsDefault;
        account.IsActive = request.IsActive;
    }

    public static CompanyPaymentAccountResponse ToResponse(CompanyPaymentAccount account)
    {
        return new CompanyPaymentAccountResponse
        {
            Id = account.Id,
            OrganizationId = account.OrganizationId,
            BankId = account.BankId,
            BankName = account.Bank.Name,
            AccountNumber = account.AccountNumber,
            AccountType = account.AccountType,
            AccountHolderName = account.AccountHolderName,
            Currency = account.Currency,
            ReservationPaymentAmount = account.ReservationPaymentAmount,
            QrBlobContainer = account.QrBlobContainer,
            QrBlobName = account.QrBlobName,
            QrBlobUri = account.QrBlobUri,
            IsDefault = account.IsDefault,
            IsActive = account.IsActive,
            CreatedAt = account.CreatedAt,
            UpdatedAt = account.UpdatedAt,
        };
    }

    public static string NormalizeCurrency(string currency)
    {
        return currency.Trim().ToUpperInvariant();
    }

    private static string NormalizeCountryCode(string countryCode)
    {
        return countryCode.Trim().ToUpperInvariant();
    }

    private static string NormalizeName(string name)
    {
        return name.Trim();
    }

}
