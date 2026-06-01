using System.Text.Json;
using CeoAgent.ApiService.Infrastructure.Json;
using CeoAgent.ApiService.Modules.Companies.Commands;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Shared.Enums;
using CeoAgent.Shared.Request.Company;
using CeoAgent.Shared.Response.Company;
using Riok.Mapperly.Abstractions;

namespace CeoAgent.ApiService.Modules.Companies.Mappers;

[Mapper(AutoUserMappings = false, RequiredMappingStrategy = RequiredMappingStrategy.Target)]
public static partial class CompanyMapper
{
    private static readonly JsonSerializerOptions SerializerOptions = CreateOptions();

    public static Company ToEntity(CreateCompanyRequest request)
    {
        return new Company
        {
            Name = request.Name,
            TimeZoneId = request.TimeZoneId,
        };
    }

    public static RegisterCompanyChannelCommand ToCommand(CompanyChannelRequest request, Guid companyId)
    {
        return new RegisterCompanyChannelCommand(
            companyId,
            request.Provider,
            request.ProviderChannelId,
            request.Metadata,
            request.CredentialReferenceId);
    }

    public static AgentProfile ToEntity(AgentProfileRequest request, Guid companyId)
    {
        return new AgentProfile
        {
            CompanyId = companyId,
            ModelName = request.ModelName,
            DisplayName = request.DisplayName,
            Language = request.Language,
            PromptOverride = request.PromptOverride,
        };
    }

    public static void ApplyToEntity(AgentProfileRequest request, AgentProfile profile, Company company)
    {
        profile.ModelName = request.ModelName;
        profile.DisplayName = request.DisplayName;
        profile.Language = request.Language;
        profile.PromptOverride = request.PromptOverride;
        company.TimeZoneId = request.TimeZoneId;
        company.WorkingHours = request.WorkingHours.DeserializeOptional<WorkingHours>();
    }

    public static CompanyTool ToEntity(CompanyToolRequest request, Guid companyId)
    {
        return new CompanyTool
        {
            CompanyId = companyId,
            ToolKey = request.ToolKey,
            Description = request.Description,
        };
    }

    public static void ApplyToEntity(CompanyToolRequest request, CompanyTool tool)
    {
        tool.Description = request.Description;
        tool.IsEnabled = request.IsEnabled;
        tool.CredentialReferenceId = request.CredentialReferenceId;
        tool.Configuration = request.Configuration.DeserializeOptional<ToolConfiguration>();
    }

    public static IntegrationCredentialReference ToEntity(IntegrationCredentialRequest request, Guid companyId)
    {
        return new IntegrationCredentialReference
        {
            CompanyId = companyId,
            Provider = request.Provider,
            Purpose = request.Purpose,
            Reference = request.Reference,
            Metadata = request.Metadata.DeserializeOptional<CredentialMetadata>(),
        };
    }

    [MapProperty(nameof(Company.Status), nameof(CompanyResponse.Status), Use = nameof(MapCompanyStatus))]
    [MapProperty(nameof(Company.WorkingHours), nameof(CompanyResponse.WorkingHours), Use = nameof(MapWorkingHours))]
    public static partial CompanyResponse ToResponse(Company company);

    [MapProperty(nameof(CompanyChannel.Metadata), nameof(CompanyChannelResponse.Metadata), Use = nameof(MapChannelMetadata))]
    public static partial CompanyChannelResponse ToResponse(CompanyChannel channel);

    public static partial AgentProfileResponse ToResponse(AgentProfile agentProfile);

    [MapProperty(nameof(CompanyTool.Configuration), nameof(CompanyToolResponse.Configuration), Use = nameof(MapToolConfiguration))]
    public static partial CompanyToolResponse ToResponse(CompanyTool companyTool);

    [MapProperty(nameof(IntegrationCredentialReference.Metadata), nameof(IntegrationCredentialResponse.Metadata), Use = nameof(MapCredentialMetadata))]
    public static partial IntegrationCredentialResponse ToResponse(IntegrationCredentialReference credential);

    private static string MapCompanyStatus(CompanyStatus status)
    {
        return status.ToString();
    }

    private static JsonElement? MapWorkingHours(WorkingHours? document)
    {
        return SerializeJsonDocument(document);
    }

    private static JsonElement? MapChannelMetadata(ChannelMetadata document)
    {
        return SerializeJsonDocument(document);
    }

    private static JsonElement? MapToolConfiguration(ToolConfiguration? document)
    {
        return SerializeJsonDocument(document);
    }

    private static JsonElement? MapCredentialMetadata(CredentialMetadata? document)
    {
        return SerializeJsonDocument(document);
    }

    private static JsonElement? SerializeJsonDocument<TDocument>(TDocument? document)
        where TDocument : class
    {
        if (document is null)
        {
            return null;
        }

        return JsonSerializer.SerializeToElement(document, document.GetType(), SerializerOptions);
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
