namespace CeoAgent.ApiService.Infrastructure.Storage;

public sealed record FileUploadValidationOptions(
    string RequiredMessage,
    string InvalidContentTypeMessage,
    IReadOnlyCollection<string> AllowedContentTypes);
