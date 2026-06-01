using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Infrastructure.Integrations;
using CeoAgent.Infrastructure.Scheduling;
using CeoAgent.Integrations.Calendar;
using CeoAgent.Shared.Constants;
using CeoAgent.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace CeoAgent.Worker.Jobs;

/// <summary>
/// Executes Google Calendar-backed tool calls with company working-hours validation and idempotent persistence.
/// </summary>
public sealed class GoogleCalendarToolExecutor(
    CeoAgentDbContext dbContext,
    ICalendarIntegration calendarIntegration,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Checks a requested reservation slot against working hours and Google Calendar, then stores the tool result.
    /// </summary>
    public async Task<ToolExecution> CheckAvailabilityAsync(
        Guid conversationId,
        Guid companyToolId,
        Guid triggerMessageId,
        CheckAvailabilityRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var existing = await FindExistingAsync(idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var context = await LoadContextAsync(conversationId, companyToolId, cancellationToken);
        var config = context.Configuration;
        var preferredTime = request.PreferredTime
            ?? GoogleCalendarSchedulingPolicy.FirstWorkingTime(context.Company.WorkingHours, request.Date);
        if (!GoogleCalendarSchedulingPolicy.IsWithinAdvanceWindow(
            request.Date,
            context.Company.TimeZoneId,
            timeProvider.GetUtcNow(),
            config.AdvanceBookingDays))
        {
            return await PersistExecutionAsync(
                context,
                triggerMessageId,
                MvpToolKeys.CheckGoogleCalendarAvailability,
                idempotencyKey,
                ToolExecutionRequest.ForCheckGoogleCalendarAvailability(request),
                ToolExecutionResult.ForCheckGoogleCalendarAvailability(new CheckAvailabilityResult
                {
                    Available = false,
                    UnavailabilityReason = "outside_advance_booking_window",
                }),
                ToolExecutionStatus.Succeeded,
                failureReason: null,
                cancellationToken);
        }

        if (preferredTime is null)
        {
            return await PersistExecutionAsync(
                context,
                triggerMessageId,
                MvpToolKeys.CheckGoogleCalendarAvailability,
                idempotencyKey,
                ToolExecutionRequest.ForCheckGoogleCalendarAvailability(request),
                ToolExecutionResult.ForCheckGoogleCalendarAvailability(new CheckAvailabilityResult
                {
                    Available = false,
                    UnavailabilityReason = "outside_working_hours",
                }),
                ToolExecutionStatus.Succeeded,
                failureReason: null,
                cancellationToken);
        }

        var start = GoogleCalendarSchedulingPolicy.ToCompanyLocalOffset(request.Date, preferredTime.Value, context.Company.TimeZoneId);
        var end = start.AddMinutes(config.ReservationMinutes);
        var alternatives = GoogleCalendarSchedulingPolicy.BuildAlternativeStarts(
            context.Company.WorkingHours,
            request.Date,
            start,
            config.SlotMinutes,
            config.ReservationMinutes,
            config.BufferMinutes);

        if (!GoogleCalendarSchedulingPolicy.IsWithinWorkingHours(context.Company.WorkingHours, start, end, config.BufferMinutes))
        {
            return await PersistExecutionAsync(
                context,
                triggerMessageId,
                MvpToolKeys.CheckGoogleCalendarAvailability,
                idempotencyKey,
                ToolExecutionRequest.ForCheckGoogleCalendarAvailability(request),
                ToolExecutionResult.ForCheckGoogleCalendarAvailability(new CheckAvailabilityResult
                {
                    Available = false,
                    AlternativeSlots = alternatives.Select(value => TimeOnly.FromDateTime(value.DateTime)).Take(1).ToList(),
                    UnavailabilityReason = "outside_working_hours",
                }),
                ToolExecutionStatus.Succeeded,
                failureReason: null,
                cancellationToken);
        }

        var calendarRequest = new CalendarAvailabilityRequest(
            context.CredentialReference,
            config.CalendarId,
            start,
            end,
            request.PartySize,
            alternatives,
            config.BufferMinutes);

        var calendarResult = await calendarIntegration.CheckAvailabilityAsync(calendarRequest, cancellationToken);
        var result = new CheckAvailabilityResult
        {
            Available = calendarResult.Available,
            AlternativeSlots = [.. calendarResult.AlternativeStarts.Select(value => TimeOnly.FromDateTime(value.DateTime))],
            UnavailabilityReason = calendarResult.UnavailabilityReason,
        };

        return await PersistExecutionAsync(
            context,
            triggerMessageId,
            MvpToolKeys.CheckGoogleCalendarAvailability,
            idempotencyKey,
            ToolExecutionRequest.ForCheckGoogleCalendarAvailability(request),
            ToolExecutionResult.ForCheckGoogleCalendarAvailability(result),
            ToolExecutionStatus.Succeeded,
            failureReason: null,
            cancellationToken);
    }

    /// <summary>
    /// Creates a calendar reservation only when it falls within working hours, then stores the tool result.
    /// </summary>
    public async Task<ToolExecution> CreateReservationAsync(
        Guid conversationId,
        Guid companyToolId,
        Guid triggerMessageId,
        CreateCalendarEventRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var existing = await FindExistingAsync(idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var context = await LoadContextAsync(conversationId, companyToolId, cancellationToken);
        var config = context.Configuration;
        if (!GoogleCalendarSchedulingPolicy.IsWithinAdvanceWindow(
            DateOnly.FromDateTime(request.Start.DateTime),
            context.Company.TimeZoneId,
            timeProvider.GetUtcNow(),
            config.AdvanceBookingDays))
        {
            return await PersistExecutionAsync(
                context,
                triggerMessageId,
                MvpToolKeys.CreateGoogleCalendarReservation,
                idempotencyKey,
                ToolExecutionRequest.ForCreateGoogleCalendarReservation(request),
                result: null,
                ToolExecutionStatus.Denied,
                "outside_advance_booking_window",
                cancellationToken);
        }

        if (!GoogleCalendarSchedulingPolicy.IsWithinWorkingHours(
            context.Company.WorkingHours,
            request.Start,
            request.End,
            config.BufferMinutes))
        {
            return await PersistExecutionAsync(
                context,
                triggerMessageId,
                MvpToolKeys.CreateGoogleCalendarReservation,
                idempotencyKey,
                ToolExecutionRequest.ForCreateGoogleCalendarReservation(request),
                result: null,
                ToolExecutionStatus.Denied,
                "outside_working_hours",
                cancellationToken);
        }

        var calendarResult = await calendarIntegration.CreateReservationAsync(
            new CalendarReservationRequest(
                CredentialReference: context.CredentialReference,
                CalendarId: config.CalendarId,
                Start: request.Start,
                End: request.End,
                Summary: request.Summary,
                IdempotencyKey: idempotencyKey,
                Description: null),
            cancellationToken: cancellationToken);

        return await PersistExecutionAsync(
            context,
            triggerMessageId,
            MvpToolKeys.CreateGoogleCalendarReservation,
            idempotencyKey,
            ToolExecutionRequest.ForCreateGoogleCalendarReservation(request),
            ToolExecutionResult.ForCreateGoogleCalendarReservation(new CreateCalendarEventResult
            {
                EventId = calendarResult.EventId,
                EventUrl = calendarResult.EventUrl,
            }),
            ToolExecutionStatus.Succeeded,
            failureReason: null,
            cancellationToken);
    }

    private async Task<ToolExecution?> FindExistingAsync(string idempotencyKey, CancellationToken cancellationToken)
    {
        return await dbContext.ToolExecutions
            .Where(entity => entity.IdempotencyKey == idempotencyKey)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<CalendarToolContext> LoadContextAsync(
        Guid conversationId,
        Guid companyToolId,
        CancellationToken cancellationToken)
    {
        var conversation = await dbContext.Conversations.FindAsync([conversationId], cancellationToken)
            ?? throw new InvalidOperationException($"Conversation '{conversationId}' was not found.");
        var company = await dbContext.Companies.FindAsync([conversation.CompanyId], cancellationToken)
            ?? throw new InvalidOperationException($"Company '{conversation.CompanyId}' was not found.");
        var tool = await dbContext.CompanyTools
            .Include(entity => entity.CredentialReference)
            .SingleOrDefaultAsync(
                entity => entity.Id == companyToolId
                    && entity.CompanyId == conversation.CompanyId
                    && entity.IsEnabled,
                cancellationToken)
            ?? throw new InvalidOperationException($"Company tool '{companyToolId}' was not found.");

        var config = tool.Configuration?.GoogleCalendar
            ?? throw new InvalidOperationException("Google Calendar tool configuration is required.");
        GoogleCalendarConfigValidator.Validate(config);

        if (tool.CredentialReference is null)
        {
            throw new InvalidOperationException("Google Calendar credential reference is required.");
        }

        var credentialReference = GoogleCalendarCredentialMaterialResolver.Resolve(tool.CredentialReference);
        return new CalendarToolContext(conversation, company, tool, config, credentialReference);
    }

    private async Task<ToolExecution> PersistExecutionAsync(
        CalendarToolContext context,
        Guid triggerMessageId,
        string toolKey,
        string idempotencyKey,
        ToolExecutionRequest request,
        ToolExecutionResult? result,
        ToolExecutionStatus status,
        string? failureReason,
        CancellationToken cancellationToken)
    {
        var execution = new ToolExecution
        {
            CompanyId = context.Company.Id,
            ConversationId = context.Conversation.Id,
            CompanyToolId = context.Tool.Id,
            TriggerMessageId = triggerMessageId,
            ToolKey = toolKey,
            IdempotencyKey = idempotencyKey,
            Status = status,
            Request = request,
            Result = result,
            FailureReason = failureReason,
        };

        if (result is not null)
        {
            var resultMessage = new Message
            {
                CompanyId = context.Company.Id,
                ConversationId = context.Conversation.Id,
                Role = MessageRole.ToolResult,
                Type = MessageType.Text,
                MessageText = toolKey,
                OccurredAt = timeProvider.GetUtcNow().UtcDateTime,
            };

            dbContext.Messages.Add(resultMessage);
            execution.ResultMessageId = resultMessage.Id;
        }

        dbContext.ToolExecutions.Add(execution);
        await dbContext.SaveChangesAsync(cancellationToken);
        return execution;
    }

    private sealed record CalendarToolContext(
        Conversation Conversation,
        Company Company,
        CompanyTool Tool,
        GoogleCalendarConfig Configuration,
        string CredentialReference);
}
