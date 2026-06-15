using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CeoAgent.ApiService.Tests.Support;
using CeoAgent.Application.Abstractions.Storage;
using CeoAgent.Shared.Response.Company;
using CeoAgent.Shared.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shouldly;

namespace CeoAgent.ApiService.Tests;

[NotInParallel]
public sealed class PaymentAdminEndpointTests
{
    [Test]
    public async Task BankEndpoints_CreateUpdateAndListOnlyActiveBanks()
    {
        await using var factory = CreateFactory(out _);
        using var client = factory.CreateAuthenticatedClient();

        var bankId = await CreateBankAsync(client, "Banco Uno", "CO");
        using var update = await client.PutAsJsonAsync(
            $"/v1/admin/banks/{bankId}",
            new
            {
                name = "Banco Uno Actualizado",
                countryCode = "CO",
                isActive = false,
            });
        update.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var inactiveList = await client.GetAsync("/v1/admin/banks");
        inactiveList.StatusCode.ShouldBe(HttpStatusCode.OK);
        var inactiveBanks = await JsonDocument.ParseAsync(await inactiveList.Content.ReadAsStreamAsync());
        inactiveBanks.RootElement.GetProperty("banks").GetArrayLength().ShouldBe(0);

        using var activate = await client.PutAsJsonAsync(
            $"/v1/admin/banks/{bankId}",
            new
            {
                name = "Banco Uno Actualizado",
                countryCode = "CO",
                isActive = true,
            });
        activate.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var activeList = await client.GetAsync("/v1/admin/banks");
        var activeBanks = await JsonDocument.ParseAsync(await activeList.Content.ReadAsStreamAsync());
        var bank = activeBanks.RootElement.GetProperty("banks").EnumerateArray().Single();
        bank.GetProperty("id").GetGuid().ShouldBe(bankId);
        bank.GetProperty("name").GetString().ShouldBe("Banco Uno Actualizado");
    }

    [Test]
    public async Task PaymentAccountEndpoints_FilterByAuthenticatedOrganizationAndDoNotReturnPublicQrUrl()
    {
        await using var factory = CreateFactory(out _);
        using var bootstrapClient = factory.CreateAuthenticatedClient();
        var bankId = await CreateBankAsync(bootstrapClient, "Banco Uno", "CO");

        var organizationA = Guid.CreateVersion7();
        var organizationB = Guid.CreateVersion7();
        using var clientA = factory.CreateAuthenticatedClient(organizationA);
        using var clientB = factory.CreateAuthenticatedClient(organizationB);
        organizationA = await CreateCompanyAsync(clientA, "Organization A");
        organizationB = await CreateCompanyAsync(clientB, "Organization B");

        var accountA = await CreatePaymentAccountAsync(clientA, bankId, "001", isDefault: true);
        _ = await CreatePaymentAccountAsync(clientB, bankId, "999", isDefault: true);

        using var list = await clientA.GetAsync("/v1/admin/payment-accounts");
        list.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = await JsonDocument.ParseAsync(await list.Content.ReadAsStreamAsync());
        var accounts = document.RootElement.GetProperty("accounts").EnumerateArray().ToArray();
        accounts.Length.ShouldBe(1);
        accounts[0].GetProperty("id").GetGuid().ShouldBe(accountA);
        accounts[0].GetProperty("organizationId").GetGuid().ShouldBe(organizationA);
        accounts[0].GetProperty("accountNumber").GetString().ShouldBe("001");
        accounts[0].TryGetProperty("qrUrl", out _).ShouldBeFalse();
    }

    [Test]
    public async Task MarkDefault_ClearsExistingDefaultForSameOrganizationAndCurrency()
    {
        await using var factory = CreateFactory(out _);
        using var bootstrapClient = factory.CreateAuthenticatedClient();
        var bankId = await CreateBankAsync(bootstrapClient, "Banco Uno", "CO");
        var organizationId = await CreateCompanyAsync(bootstrapClient, "Organization A");
        using var client = factory.CreateAuthenticatedClient(organizationId);

        var first = await CreatePaymentAccountAsync(client, bankId, "001", isDefault: true);
        var second = await CreatePaymentAccountAsync(client, bankId, "002", isDefault: false);

        using var markDefault = await client.PostAsync($"/v1/admin/payment-accounts/{second}/default", content: null);
        markDefault.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var list = await client.GetAsync("/v1/admin/payment-accounts");
        using var document = await JsonDocument.ParseAsync(await list.Content.ReadAsStreamAsync());
        var accounts = document.RootElement.GetProperty("accounts").EnumerateArray().ToArray();
        accounts.Single(account => account.GetProperty("id").GetGuid() == first).GetProperty("isDefault").GetBoolean().ShouldBeFalse();
        accounts.Single(account => account.GetProperty("id").GetGuid() == second).GetProperty("isDefault").GetBoolean().ShouldBeTrue();
    }

    [Test]
    public async Task SetPaymentAccountActive_WhenDeactivatingDefault_RemovesItFromDefault()
    {
        await using var factory = CreateFactory(out _);
        using var bootstrapClient = factory.CreateAuthenticatedClient();
        var bankId = await CreateBankAsync(bootstrapClient, "Banco Uno", "CO");
        var organizationId = await CreateCompanyAsync(bootstrapClient, "Organization A");
        using var client = factory.CreateAuthenticatedClient(organizationId);
        var accountId = await CreatePaymentAccountAsync(client, bankId, "001", isDefault: true);

        using var deactivate = await client.PatchAsJsonAsync(
            $"/v1/admin/payment-accounts/{accountId}/active",
            new { isActive = false });
        deactivate.StatusCode.ShouldBe(HttpStatusCode.OK);

        using var list = await client.GetAsync("/v1/admin/payment-accounts");
        using var document = await JsonDocument.ParseAsync(await list.Content.ReadAsStreamAsync());
        var account = document.RootElement.GetProperty("accounts").EnumerateArray().Single();
        account.GetProperty("isActive").GetBoolean().ShouldBeFalse();
        account.GetProperty("isDefault").GetBoolean().ShouldBeFalse();
    }

    [Test]
    public async Task CreatePaymentAccount_UploadsQrImageToGeneratedPrivatePaymentBlobReference()
    {
        await using var factory = CreateFactory(out var blobStorage);
        using var bootstrapClient = factory.CreateAuthenticatedClient();
        var bankId = await CreateBankAsync(bootstrapClient, "Banco Uno", "CO");
        var organizationId = await CreateCompanyAsync(bootstrapClient, "Cafe del Mar S.A.");
        using var client = factory.CreateAuthenticatedClient(organizationId);

        var accountId = await CreatePaymentAccountAsync(client, bankId, "001", isDefault: true);

        using var list = await client.GetAsync("/v1/admin/payment-accounts");
        list.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var document = await JsonDocument.ParseAsync(await list.Content.ReadAsStreamAsync());
        var account = document.RootElement.GetProperty("accounts").EnumerateArray().Single();
        account.GetProperty("id").GetGuid().ShouldBe(accountId);
        account.GetProperty("qrBlobContainer").GetString().ShouldBe(BlobStorageContainerNames.Private);
        var expectedBlobName = $"qr-{accountId:D}.png";
        var expectedBlobUri = $"https://storage.test/{BlobStorageContainerNames.Private}/{expectedBlobName}";
        account.GetProperty("qrBlobName").GetString().ShouldBe(expectedBlobName);
        account.GetProperty("qrBlobUri").GetString().ShouldBe(expectedBlobUri);

        var upload = blobStorage.Uploads.Single();
        upload.Reference.ContainerName.ShouldBe(BlobStorageContainerNames.Private);
        upload.Reference.BlobName.ShouldBe(expectedBlobName);
        upload.BlobUri.ShouldBe(expectedBlobUri);
        upload.ContentType.ShouldBe("image/png");
        upload.Content.ShouldBe([1, 2, 3, 4]);
        upload.Tags.ShouldContainKeyAndValue("category", "payment_qr");
        upload.Tags.ShouldContainKeyAndValue("payment_account_id", accountId.ToString("D"));
    }

    [Test]
    public async Task CreatePaymentAccount_WhenQrImageMissing_ReturnsBadRequest()
    {
        await using var factory = CreateFactory(out var blobStorage);
        using var bootstrapClient = factory.CreateAuthenticatedClient();
        var bankId = await CreateBankAsync(bootstrapClient, "Banco Uno", "CO");
        var organizationId = await CreateCompanyAsync(bootstrapClient, "Organization A");
        using var client = factory.CreateAuthenticatedClient(organizationId);

        using var response = await client.PostAsync(
            "/v1/admin/payment-accounts",
            CreatePaymentAccountMultipartContent(bankId, "001", isDefault: true, includeQrImage: false));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        blobStorage.Uploads.ShouldBeEmpty();
    }

    [Test]
    public async Task CreatePaymentAccount_WhenAccountTypeUnsupported_ReturnsBadRequest()
    {
        await using var factory = CreateFactory(out var blobStorage);
        using var bootstrapClient = factory.CreateAuthenticatedClient();
        var bankId = await CreateBankAsync(bootstrapClient, "Banco Uno", "CO");
        var organizationId = await CreateCompanyAsync(bootstrapClient, "Organization A");
        using var client = factory.CreateAuthenticatedClient(organizationId);

        using var response = await client.PostAsync(
            "/v1/admin/payment-accounts",
            CreatePaymentAccountMultipartContent(
                bankId,
                "001",
                isDefault: true,
                includeQrImage: true,
                accountType: "Payroll"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        blobStorage.Uploads.ShouldBeEmpty();
    }

    [Test]
    public async Task UpdatePaymentAccount_WhenQrImageProvided_UploadsReplacementToExistingBlobReference()
    {
        await using var factory = CreateFactory(out var blobStorage);
        using var bootstrapClient = factory.CreateAuthenticatedClient();
        var bankId = await CreateBankAsync(bootstrapClient, "Banco Uno", "CO");
        var organizationId = await CreateCompanyAsync(bootstrapClient, "Organization A");
        using var client = factory.CreateAuthenticatedClient(organizationId);
        var accountId = await CreatePaymentAccountAsync(client, bankId, "001", isDefault: true);
        blobStorage.Uploads.Clear();

        using var response = await client.PutAsync(
            $"/v1/admin/payment-accounts/{accountId}",
            CreatePaymentAccountMultipartContent(bankId, "001", isDefault: true, includeQrImage: true));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var upload = blobStorage.Uploads.Single();
        upload.Reference.ContainerName.ShouldBe(BlobStorageContainerNames.Private);
        upload.Reference.BlobName.ShouldBe($"qr-{accountId:D}.png");
        upload.BlobUri.ShouldBe($"https://storage.test/{BlobStorageContainerNames.Private}/qr-{accountId:D}.png");
        upload.ContentType.ShouldBe("image/png");
    }

    private static async Task<Guid> CreateBankAsync(HttpClient client, string name, string countryCode)
    {
        using var response = await client.PostAsJsonAsync(
            "/v1/admin/banks",
            new
            {
                name,
                countryCode,
                isActive = true,
            });
        response.EnsureSuccessStatusCode();
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static async Task<Guid> CreatePaymentAccountAsync(
        HttpClient client,
        Guid bankId,
        string accountNumber,
        bool isDefault)
    {
        using var response = await client.PostAsync(
            "/v1/admin/payment-accounts",
            CreatePaymentAccountMultipartContent(bankId, accountNumber, isDefault, includeQrImage: true));
        response.EnsureSuccessStatusCode();
        using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
        return document.RootElement.GetProperty("id").GetGuid();
    }

    private static MultipartFormDataContent CreatePaymentAccountMultipartContent(
        Guid bankId,
        string accountNumber,
        bool isDefault,
        bool includeQrImage,
        string accountType = "Ahorros")
    {
        var content = new MultipartFormDataContent
        {
            { new StringContent(bankId.ToString("D")), "bankId" },
            { new StringContent(accountNumber), "accountNumber" },
            { new StringContent(accountType), "accountType" },
            { new StringContent("Contoso Bistro"), "accountHolderName" },
            { new StringContent("COP"), "currency" },
            { new StringContent("50000"), "reservationPaymentAmount" },
            { new StringContent(isDefault.ToString()), "isDefault" },
            { new StringContent("true"), "isActive" },
        };

        if (includeQrImage)
        {
            var qrImage = new ByteArrayContent([1, 2, 3, 4]);
            qrImage.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            content.Add(qrImage, "qrImage", "qr.png");
        }

        return content;
    }

    private static async Task<Guid> CreateCompanyAsync(HttpClient client, string name)
    {
        using var response = await client.PostAsJsonAsync(
            "/v1/admin/companies",
            new
            {
                name,
                timeZoneId = "America/Bogota",
            });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<CompanyResponse>();
        body.ShouldNotBeNull();
        return body.Id;
    }

    private static ApiFactory CreateFactory(out FakeBlobStorageService blobStorage)
    {
        blobStorage = new FakeBlobStorageService();
        var captured = blobStorage;
        return new ApiFactory(configureServices: services =>
        {
            services.RemoveAll<IBlobStorageService>();
            services.AddSingleton<IBlobStorageService>(captured);
        });
    }

    private sealed class FakeBlobStorageService : IBlobStorageService
    {
        public List<CapturedUpload> Uploads { get; } = [];

        public async Task<BlobStorageUploadResult> UploadAsync(BlobStorageUploadRequest request, CancellationToken cancellationToken)
        {
            await using var memory = new MemoryStream();
            await request.Content.CopyToAsync(memory, cancellationToken);
            var blobUri = $"https://storage.test/{request.Reference.ContainerName}/{request.Reference.BlobName}";
            Uploads.Add(new CapturedUpload(
                request.Reference,
                blobUri,
                memory.ToArray(),
                request.ContentType,
                new Dictionary<string, string>(request.Tags, StringComparer.Ordinal)));
            return new BlobStorageUploadResult(request.Reference, blobUri);
        }

        public Task<BlobStorageDownloadResult> DownloadAsync(
            BlobStorageReference reference,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<bool> ExistsAsync(BlobStorageReference reference, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<bool> DeleteIfExistsAsync(BlobStorageReference reference, CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task SetTagsAsync(
            BlobStorageReference reference,
            IReadOnlyDictionary<string, string> tags,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyDictionary<string, string>> GetTagsAsync(
            BlobStorageReference reference,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed record CapturedUpload(
        BlobStorageReference Reference,
        string BlobUri,
        byte[] Content,
        string ContentType,
        IReadOnlyDictionary<string, string> Tags);
}
