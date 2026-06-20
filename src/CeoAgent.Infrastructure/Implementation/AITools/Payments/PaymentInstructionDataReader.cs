using CeoAgent.Application.Abstractions.AITools;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Persistence.Extensions;
using CeoAgent.Shared.Constants;
using CeoAgent.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace CeoAgent.Infrastructure.Implementation.AITools.Payments;

public sealed class PaymentInstructionDataReader(CeoAgentDbContext dbContext)
{
    public async Task<PaymentInstructionToolContext> LoadToolContextAsync(
        ToolExecutionContext executionContext,
        CancellationToken cancellationToken)
    {
        var conversation = await dbContext.Conversations
            .AsNoTracking()
            .ForOrganization(executionContext.OrganizationId)
            .SingleOrDefaultAsync(entity => entity.Id == executionContext.ConversationId, cancellationToken)
            ?? throw new InvalidOperationException($"Conversation '{executionContext.ConversationId}' was not found.");
        var customer = await dbContext.Customers
            .AsNoTracking()
            .ForOrganization(executionContext.OrganizationId)
            .SingleOrDefaultAsync(entity => entity.Id == conversation.CustomerId, cancellationToken)
            ?? throw new InvalidOperationException($"Customer '{conversation.CustomerId}' was not found.");
        var tool = await dbContext.CompanyTools
            .AsNoTracking()
            .EnabledForOrganizationTool(executionContext.OrganizationId, executionContext.CompanyToolId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException($"Company tool '{executionContext.CompanyToolId}' was not found.");

        if (!string.Equals(tool.ToolKey, MvpToolKeys.SendPaymentInstructions, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Company tool '{tool.Id}' is not '{MvpToolKeys.SendPaymentInstructions}'.");
        }

        return new PaymentInstructionToolContext(conversation, customer, tool);
    }

    public async Task<ToolExecution?> FindLatestSuccessfulReservationExecutionAsync(
        PaymentInstructionToolContext context,
        CancellationToken cancellationToken)
    {
        var executions = await dbContext.ToolExecutions
            .AsNoTracking()
            .ForOrganization(context.Conversation.OrganizationId)
            .Where(execution => execution.ConversationId == context.Conversation.Id
                && execution.ToolKey == MvpToolKeys.CreateGoogleCalendarReservation
                && execution.Status == ToolExecutionStatus.ToolExecutionSucceeded)
            .OrderByDescending(execution => execution.CreatedAt)
            .ThenByDescending(execution => execution.Id)
            .ToListAsync(cancellationToken);

        return executions.FirstOrDefault(execution => execution.Result?.CreateCalendarEvent is not null);
    }

    public async Task<CompanyPaymentAccount?> FindDefaultActivePaymentAccountAsync(
        Guid organizationId,
        CancellationToken cancellationToken)
    {
        return await dbContext.CompanyPaymentAccounts
            .AsNoTracking()
            .WithBank()
            .ForOrganization(organizationId)
            .ActiveDefaults()
            .OrderBy(entity => entity.Currency)
            .ThenBy(entity => entity.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public static bool IsConfigured(CompanyPaymentAccount account)
    {
        return account.Bank.IsActive
            && !string.IsNullOrWhiteSpace(account.AccountNumber)
            && Enum.IsDefined(account.AccountType)
            && !string.IsNullOrWhiteSpace(account.Currency)
            && account.ReservationPaymentAmount > 0
            && !string.IsNullOrWhiteSpace(account.QrBlobContainer)
            && !string.IsNullOrWhiteSpace(account.QrBlobName);
    }
}

public sealed record PaymentInstructionToolContext(
    Conversation Conversation,
    Customer Customer,
    CompanyTool Tool);
