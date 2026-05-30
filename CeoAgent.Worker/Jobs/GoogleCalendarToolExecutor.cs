using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
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
    private const int DefaultReservationMinutes = 60;
    private const int DefaultSlotMinutes = 30;

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
        var preferredTime = request.PreferredTime ?? FirstWorkingTime(context.Company.WorkingHours, request.Date);
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

        var start = ToCompanyLocalOffset(request.Date, preferredTime.Value, context.Company.TimeZoneId);
        var end = start.AddMinutes(DefaultReservationMinutes);
        var alternatives = BuildAlternativeStarts(context.Company.WorkingHours, request.Date, start, DefaultSlotMinutes);

        if (!IsWithinWorkingHours(context.Company.WorkingHours, start, end))
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
            CredentialReference(context.Tool),
            CalendarConfig(context.Tool).CalendarId,
            start,
            end,
            request.PartySize,
            alternatives);

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
        if (!IsWithinWorkingHours(context.Company.WorkingHours, request.Start, request.End))
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
                CredentialReference: CredentialReference(context.Tool),
                CalendarId: CalendarConfig(context.Tool).CalendarId,
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
        var tool = await dbContext.CompanyTools.FindAsync([companyToolId], cancellationToken)
            ?? throw new InvalidOperationException($"Company tool '{companyToolId}' was not found.");

        if (tool.CompanyId != conversation.CompanyId)
        {
            throw new InvalidOperationException("Company tool does not belong to the conversation company.");
        }

        return new CalendarToolContext(conversation, company, tool);
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

    private static GoogleCalendarConfig CalendarConfig(CompanyTool tool)
    {
        return tool.Configuration?.GoogleCalendar
            ?? throw new InvalidOperationException("Google Calendar tool configuration is required.");
    }

    private static string CredentialReference(CompanyTool tool)
    {
        return tool.CredentialReference?.Reference ?? "default";
    }

    private static DateTimeOffset ToCompanyLocalOffset(DateOnly date, TimeOnly time, string timeZoneId)
    {
        var localDateTime = date.ToDateTime(time);
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var offset = timeZone.GetUtcOffset(localDateTime);
        return new DateTimeOffset(localDateTime, offset);
    }

    private static TimeOnly? FirstWorkingTime(WorkingHours? workingHours, DateOnly date)
    {
        return SlotsForDate(workingHours, date)
            .OrderBy(slot => slot.Start)
            .Select(slot => (TimeOnly?)slot.Start)
            .FirstOrDefault();
    }

    private static bool IsWithinWorkingHours(WorkingHours? workingHours, DateTimeOffset start, DateTimeOffset end)
    {
        var date = DateOnly.FromDateTime(start.DateTime);
        var startTime = TimeOnly.FromDateTime(start.DateTime);
        var endTime = TimeOnly.FromDateTime(end.DateTime);

        return SlotsForDate(workingHours, date)
            .Any(slot => startTime >= slot.Start && endTime <= slot.End);
    }

    private static DateTimeOffset[] BuildAlternativeStarts(
        WorkingHours? workingHours,
        DateOnly date,
        DateTimeOffset requestedStart,
        int slotMinutes)
    {
        var alternatives = new List<DateTimeOffset>();
        foreach (var slot in SlotsForDate(workingHours, date).OrderBy(slot => slot.Start))
        {
            var cursor = new DateTimeOffset(date.ToDateTime(slot.Start), requestedStart.Offset);
            var latestStart = new DateTimeOffset(date.ToDateTime(slot.End), requestedStart.Offset).AddMinutes(-DefaultReservationMinutes);
            while (cursor <= latestStart)
            {
                if (cursor != requestedStart)
                {
                    alternatives.Add(cursor);
                }

                cursor = cursor.AddMinutes(slotMinutes);
            }
        }

        return alternatives
            .OrderBy(value => Math.Abs((value - requestedStart).TotalMinutes))
            .ThenBy(value => value)
            .ToArray();
    }

    private static List<TimeSlot> SlotsForDate(WorkingHours? workingHours, DateOnly date)
    {
        if (workingHours is null)
        {
            return [];
        }

        var specialDay = workingHours.Holidays.FirstOrDefault(day => day.Date == date);
        if (specialDay is { IsClosed: true })
        {
            return [];
        }

        if (specialDay is not null)
        {
            return specialDay.TimeSlots;
        }

        return date.DayOfWeek switch
        {
            DayOfWeek.Monday => workingHours.Schedule.Monday,
            DayOfWeek.Tuesday => workingHours.Schedule.Tuesday,
            DayOfWeek.Wednesday => workingHours.Schedule.Wednesday,
            DayOfWeek.Thursday => workingHours.Schedule.Thursday,
            DayOfWeek.Friday => workingHours.Schedule.Friday,
            DayOfWeek.Saturday => workingHours.Schedule.Saturday,
            DayOfWeek.Sunday => workingHours.Schedule.Sunday,
            _ => [],
        };
    }

    private sealed record CalendarToolContext(
        Conversation Conversation,
        Company Company,
        CompanyTool Tool);
}
