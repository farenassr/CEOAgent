using Microsoft.Extensions.Logging;

namespace CeoAgent.Infrastructure.Implementation.AITools.Payments;

public sealed partial class PaymentInstructionDispatchService
{
    [LoggerMessage(
        EventId = 6010,
        Level = LogLevel.Warning,
        Message = "PaymentQrImageSendFailed OrganizationId={OrganizationId} ConversationId={ConversationId} ToolExecutionId={ToolExecutionId}")]
    private static partial void PaymentQrImageSendFailed(
        ILogger logger,
        Exception exception,
        Guid organizationId,
        Guid conversationId,
        Guid toolExecutionId);
}
