using CeoAgent.Infrastructure;
using CeoAgent.Infrastructure.Entities;
using CeoAgent.Infrastructure.Entities.JsonDocuments;
using CeoAgent.Infrastructure.Implementation.AITools.GoogleCalendar.Integration;
using CeoAgent.Infrastructure.Persistence.Extensions;
using CeoAgent.Application.Abstractions.AITools.GoogleCalendar;
using CeoAgent.Shared.Calendar;
using CeoAgent.Shared.Constants;
using CeoAgent.Shared.Enums;
using CeoAgent.Infrastructure.Implementation.AITools.Execution;
using Microsoft.EntityFrameworkCore;
using InfrastructureCompany = CeoAgent.Infrastructure.Entities.Company;

namespace CeoAgent.Infrastructure.Implementation.AITools.GoogleCalendar;

/// <summary>
/// Executes Google Calendar-backed tool calls with company working-hours validation and idempotent persistence.
/// </summary>
public sealed class GoogleCalendarToolExecutor(
    CeoAgentDbContext dbContext,
    IGoogleCalendarIntegration calendarIntegration,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Checks a requested reservation slot against working hours and Google Calendar, then stores the tool result.
    /// </summary>
    public async Task<ToolExecution> CheckAvailabilityAsync(
        Guid companyId,
        Guid conversationId,
        Guid companyToolId,
        Guid triggerMessageId,
        CheckAvailabilityRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = await LoadContextAsync(companyId, conversationId, companyToolId, cancellationToken);
        var existing = await FindExistingAsync(companyId, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

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
        var searchWindowStart = start.AddHours(-GoogleCalendarSchedulingPolicy.AlternativeSearchWindowHours);
        var searchWindowEnd = start.AddHours(GoogleCalendarSchedulingPolicy.AlternativeSearchWindowHours);
        var alternatives = GoogleCalendarSchedulingPolicy.BuildAlternativeStarts(
            context.Company.WorkingHours,
            request.Date,
            start,
            config.SlotMinutes,
            config.ReservationMinutes,
            config.BufferMinutes);
        var requestedSlotEligible = GoogleCalendarSchedulingPolicy.IsWithinWorkingHours(
            context.Company.WorkingHours,
            start,
            end,
            config.BufferMinutes);

        if (!requestedSlotEligible && alternatives.Length == 0)
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

        var calendarRequest = new CalendarAvailabilityRequest(
            context.CredentialReference,
            config.CalendarId,
            start,
            end,
            searchWindowStart,
            searchWindowEnd,
            request.PartySize,
            alternatives,
            requestedSlotEligible,
            config.BufferMinutes);

        var calendarResult = await calendarIntegration.CheckAvailabilityAsync(calendarRequest, cancellationToken);
        var result = new CheckAvailabilityResult
        {
            Available = requestedSlotEligible && calendarResult.Available,
            AlternativeSlots = [.. calendarResult.AlternativeStarts.Select(value => TimeOnly.FromDateTime(value.DateTime))],
            UnavailabilityReason = requestedSlotEligible ? calendarResult.UnavailabilityReason : "outside_working_hours",
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
        Guid companyId,
        Guid conversationId,
        Guid companyToolId,
        Guid triggerMessageId,
        CreateCalendarEventRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = await LoadContextAsync(companyId, conversationId, companyToolId, cancellationToken);
        var existing = await FindExistingAsync(companyId, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

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
                Description: $"Customer: {request.CustomerName.Trim()}",
                CustomerEmail: null,
                CompanyId: context.Company.Id.ToString("D"),
                ConversationId: context.Conversation.Id.ToString("D"),
                CustomerExternalId: context.Customer.ExternalCustomerId,
                ReservationId: idempotencyKey),
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

    public async Task<ToolExecution> FindReservationsAsync(
        Guid companyId,
        Guid conversationId,
        Guid companyToolId,
        Guid triggerMessageId,
        FindGoogleCalendarReservationsRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = await LoadContextAsync(companyId, conversationId, companyToolId, cancellationToken);
        var existing = await FindExistingAsync(companyId, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var window = BuildSearchWindow(context.Company.TimeZoneId, request.Date, request.IncludePast);
        var calendarResult = await calendarIntegration.FindReservationsAsync(
            new CalendarReservationSearchRequest(
                context.CredentialReference,
                context.Configuration.CalendarId,
                context.Company.Id.ToString("D"),
                context.Customer.ExternalCustomerId,
                window.Start,
                window.End,
                request.IncludePast),
            cancellationToken);
        if (calendarResult.FailureReason is not null)
        {
            return await PersistExecutionAsync(
                context,
                triggerMessageId,
                MvpToolKeys.FindGoogleCalendarReservations,
                idempotencyKey,
                ToolExecutionRequest.ForFindGoogleCalendarReservations(request),
                result: null,
                ToolExecutionStatus.Failed,
                calendarResult.FailureReason,
                cancellationToken);
        }

        var reservations = calendarResult.Reservations.Select(ToResultItem).ToList();
        var result = new FindGoogleCalendarReservationsResult
        {
            Reservations = reservations,
            Count = reservations.Count,
            DisambiguationNeeded = reservations.Count > 1,
        };

        return await PersistExecutionAsync(
            context,
            triggerMessageId,
            MvpToolKeys.FindGoogleCalendarReservations,
            idempotencyKey,
            ToolExecutionRequest.ForFindGoogleCalendarReservations(request),
            ToolExecutionResult.ForFindGoogleCalendarReservations(result),
            ToolExecutionStatus.Succeeded,
            failureReason: null,
            cancellationToken);
    }

    public async Task<ToolExecution> UpdateReservationAsync(
        Guid companyId,
        Guid conversationId,
        Guid companyToolId,
        Guid triggerMessageId,
        UpdateGoogleCalendarReservationRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = await LoadContextAsync(companyId, conversationId, companyToolId, cancellationToken);
        var existing = await FindExistingAsync(companyId, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        if (!GoogleCalendarSchedulingPolicy.IsWithinAdvanceWindow(
            DateOnly.FromDateTime(request.NewStart.DateTime),
            context.Company.TimeZoneId,
            timeProvider.GetUtcNow(),
            context.Configuration.AdvanceBookingDays))
        {
            return await PersistExecutionAsync(
                context,
                triggerMessageId,
                MvpToolKeys.UpdateGoogleCalendarReservation,
                idempotencyKey,
                ToolExecutionRequest.ForUpdateGoogleCalendarReservation(request),
                result: null,
                ToolExecutionStatus.Denied,
                "outside_advance_booking_window",
                cancellationToken);
        }

        if (!GoogleCalendarSchedulingPolicy.IsWithinWorkingHours(
            context.Company.WorkingHours,
            request.NewStart,
            request.NewEnd,
            context.Configuration.BufferMinutes))
        {
            return await PersistExecutionAsync(
                context,
                triggerMessageId,
                MvpToolKeys.UpdateGoogleCalendarReservation,
                idempotencyKey,
                ToolExecutionRequest.ForUpdateGoogleCalendarReservation(request),
                result: null,
                ToolExecutionStatus.Denied,
                "outside_working_hours",
                cancellationToken);
        }

        var calendarResult = await calendarIntegration.UpdateReservationAsync(
            new CalendarReservationUpdateRequest(
                context.CredentialReference,
                context.Configuration.CalendarId,
                context.Company.Id.ToString("D"),
                context.Customer.ExternalCustomerId,
                request.ReservationId,
                request.NewStart,
                request.NewEnd,
                request.Summary,
                request.CustomerName,
                context.Configuration.BufferMinutes),
            cancellationToken);

        if (!calendarResult.Succeeded || calendarResult.Reservation is null)
        {
            var failureReason = calendarResult.FailureReason ?? "reservation_not_found_or_not_owned";
            return await PersistExecutionAsync(
                context,
                triggerMessageId,
                MvpToolKeys.UpdateGoogleCalendarReservation,
                idempotencyKey,
                ToolExecutionRequest.ForUpdateGoogleCalendarReservation(request),
                result: null,
                IsProviderFailureReason(failureReason) ? ToolExecutionStatus.Failed : ToolExecutionStatus.Denied,
                failureReason,
                cancellationToken);
        }

        return await PersistExecutionAsync(
            context,
            triggerMessageId,
            MvpToolKeys.UpdateGoogleCalendarReservation,
            idempotencyKey,
            ToolExecutionRequest.ForUpdateGoogleCalendarReservation(request),
            ToolExecutionResult.ForUpdateGoogleCalendarReservation(new UpdateGoogleCalendarReservationResult
            {
                Reservation = ToResultItem(calendarResult.Reservation),
            }),
            ToolExecutionStatus.Succeeded,
            failureReason: null,
            cancellationToken);
    }

    public async Task<ToolExecution> CancelReservationAsync(
        Guid companyId,
        Guid conversationId,
        Guid companyToolId,
        Guid triggerMessageId,
        CancelGoogleCalendarReservationRequest request,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var context = await LoadContextAsync(companyId, conversationId, companyToolId, cancellationToken);
        var existing = await FindExistingAsync(companyId, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        var calendarResult = await calendarIntegration.CancelReservationAsync(
            new CalendarReservationCancellationRequest(
                context.CredentialReference,
                context.Configuration.CalendarId,
                context.Company.Id.ToString("D"),
                context.Customer.ExternalCustomerId,
                request.ReservationId,
                request.Reason),
            cancellationToken);

        if (!calendarResult.Succeeded)
        {
            var failureReason = calendarResult.FailureReason ?? "reservation_not_found_or_not_owned";
            return await PersistExecutionAsync(
                context,
                triggerMessageId,
                MvpToolKeys.CancelGoogleCalendarReservation,
                idempotencyKey,
                ToolExecutionRequest.ForCancelGoogleCalendarReservation(request),
                result: null,
                IsProviderFailureReason(failureReason) ? ToolExecutionStatus.Failed : ToolExecutionStatus.Denied,
                failureReason,
                cancellationToken);
        }

        return await PersistExecutionAsync(
            context,
            triggerMessageId,
            MvpToolKeys.CancelGoogleCalendarReservation,
            idempotencyKey,
            ToolExecutionRequest.ForCancelGoogleCalendarReservation(request),
            ToolExecutionResult.ForCancelGoogleCalendarReservation(new CancelGoogleCalendarReservationResult
            {
                Cancelled = true,
                ReservationId = calendarResult.ReservationId,
                EventId = calendarResult.EventId,
            }),
            ToolExecutionStatus.Succeeded,
            failureReason: null,
            cancellationToken);
    }

    private async Task<ToolExecution?> FindExistingAsync(
        Guid companyId,
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return await dbContext.FindTrackedOrPersistedToolExecutionAsync(
            companyId,
            idempotencyKey,
            cancellationToken);
    }

    private async Task<CalendarToolContext> LoadContextAsync(
        Guid companyId,
        Guid conversationId,
        Guid companyToolId,
        CancellationToken cancellationToken)
    {
        var conversation = await dbContext.Conversations
            .AsNoTracking()
            .ForCompany(companyId)
            .SingleOrDefaultAsync(
                entity => entity.Id == conversationId,
                cancellationToken)
            ?? throw new InvalidOperationException($"Conversation '{conversationId}' was not found.");
        var customer = await dbContext.Customers
            .AsNoTracking()
            .ForCompany(companyId)
            .SingleOrDefaultAsync(
                entity => entity.Id == conversation.CustomerId,
                cancellationToken)
            ?? throw new InvalidOperationException($"Customer '{conversation.CustomerId}' was not found.");
        var company = await dbContext.Companies
            .AsNoTracking()
            .SingleOrDefaultAsync(entity => entity.Id == companyId, cancellationToken)
            ?? throw new InvalidOperationException($"Company '{companyId}' was not found.");
        var tool = await dbContext.CompanyTools
            .AsNoTracking()
            .WithCredentialReference()
            .EnabledForCompanyTool(companyId, companyToolId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException($"Company tool '{companyToolId}' was not found.");

        var config = tool.Configuration?.GoogleCalendar
            ?? throw new InvalidOperationException("Google Calendar tool configuration is required.");
        GoogleCalendarConfigValidator.Validate(config);

        if (tool.CredentialReference is null)
        {
            throw new InvalidOperationException("Google Calendar credential reference is required.");
        }

        var credentialReference = GoogleCalendarCredentialMaterialResolver.Resolve(tool.CredentialReference);
        return new CalendarToolContext(conversation, customer, company, tool, config, credentialReference);
    }

    private (DateTimeOffset Start, DateTimeOffset End) BuildSearchWindow(
        string timeZoneId,
        DateOnly? date,
        bool includePast)
    {
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var localNow = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), timeZone);
        if (date is { } localDate)
        {
            var start = GoogleCalendarSchedulingPolicy.ToCompanyLocalOffset(localDate, TimeOnly.MinValue, timeZoneId);
            return (start, start.AddDays(1));
        }

        var todayStart = GoogleCalendarSchedulingPolicy.ToCompanyLocalOffset(
            DateOnly.FromDateTime(localNow.DateTime),
            TimeOnly.MinValue,
            timeZoneId);
        var windowStart = includePast ? todayStart.AddDays(-30) : localNow;
        return (windowStart, todayStart.AddDays(30));
    }

    private static GoogleCalendarReservationResultItem ToResultItem(CalendarReservationInfo reservation)
    {
        return new GoogleCalendarReservationResultItem
        {
            ReservationId = reservation.ReservationId,
            EventId = reservation.EventId,
            Start = reservation.Start,
            End = reservation.End,
            Summary = reservation.Summary,
            CustomerName = reservation.CustomerName,
            EventUrl = reservation.EventUrl,
        };
    }

    private static bool IsProviderFailureReason(string failureReason)
    {
        return failureReason is "upstream_error" or "rate_limited" or "calendar_access_denied";
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
        return execution;
    }

    private sealed record CalendarToolContext(
        Conversation Conversation,
        Customer Customer,
        InfrastructureCompany Company,
        CompanyTool Tool,
        GoogleCalendarConfig Configuration,
        string CredentialReference);
}
