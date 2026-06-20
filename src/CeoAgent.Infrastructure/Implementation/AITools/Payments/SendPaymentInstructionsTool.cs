using CeoAgent.Application.Abstractions.AITools;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Infrastructure.Implementation.AITools.Execution;
using CeoAgent.Shared.Constants;

namespace CeoAgent.Infrastructure.Implementation.AITools.Payments;

public sealed class SendPaymentInstructionsTool(
    ReservationPaymentInstructionSender sender) : AgentTool<SendPaymentInstructionsRequest>
{
    public override string ToolKey => MvpToolKeys.SendPaymentInstructions;

    public override bool IsMutating => true;

    public override string Description => "Send the reservation payment QR image and full payment instructions for the latest successful reservation in the current conversation.";

    protected override async Task<ToolExecution> ExecuteToolAsync(
        ToolExecutionContext context,
        SendPaymentInstructionsRequest request,
        CancellationToken cancellationToken)
    {
        return await sender.SendForLatestSuccessfulReservationAsync(context, request, cancellationToken);
    }
}
