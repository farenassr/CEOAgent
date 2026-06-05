using CeoAgent.Infrastructure.Scheduling;
using CeoAgent.Integrations.Calendar;
using CeoAgent.Shared.Constants;
using CeoAgent.Shared.Request.GoogleCalendar;
using CeoAgent.Shared.Response.GoogleCalendar;
using FastEndpoints;
using FluentValidation;

namespace CeoAgent.ApiService.Modules.GoogleCalendar;

/// <summary>
/// Checks Google Calendar availability for a company-scoped calendar tool.
/// </summary>
public sealed class CheckGoogleCalendarAvailabilityEndpoint(
    GoogleCalendarCompanyToolResolver resolver,
    ICalendarIntegration calendarIntegration,
    TimeProvider timeProvider)
    : Endpoint<CheckGoogleCalendarAvailabilityRequest, GoogleCalendarAvailabilityResponse>
{
    public override void Configure()
    {
        Post("/v1/admin/companies/{companyId}/google-calendar/availability");
    }

    public override async Task HandleAsync(
        CheckGoogleCalendarAvailabilityRequest request,
        CancellationToken cancellationToken)
    {
        var companyId = Route<Guid>("companyId");
        var context = await resolver.ResolveAsync(
            companyId,
            MvpToolKeys.CheckGoogleCalendarAvailability,
            cancellationToken);

        var preferredTime = request.PreferredTime
            ?? GoogleCalendarSchedulingPolicy.FirstWorkingTime(context.Company.WorkingHours, request.Date);
        if (!GoogleCalendarSchedulingPolicy.IsWithinAdvanceWindow(
            request.Date,
            context.Company.TimeZoneId,
            timeProvider.GetUtcNow(),
            context.Configuration.AdvanceBookingDays))
        {
            await Send.OkAsync(
                new GoogleCalendarAvailabilityResponse
                {
                    Available = false,
                    UnavailabilityReason = "outside_advance_booking_window",
                },
                cancellationToken);
            return;
        }

        if (preferredTime is null)
        {
            await Send.OkAsync(
                new GoogleCalendarAvailabilityResponse
                {
                    Available = false,
                    UnavailabilityReason = "outside_working_hours",
                },
                cancellationToken);
            return;
        }

        var start = GoogleCalendarSchedulingPolicy.ToCompanyLocalOffset(request.Date, preferredTime.Value, context.Company.TimeZoneId);
        var end = start.AddMinutes(context.Configuration.ReservationMinutes);
        var alternatives = GoogleCalendarSchedulingPolicy.BuildAlternativeStarts(
            context.Company.WorkingHours,
            request.Date,
            start,
            context.Configuration.SlotMinutes,
            context.Configuration.ReservationMinutes,
            context.Configuration.BufferMinutes);

        if (!GoogleCalendarSchedulingPolicy.IsWithinWorkingHours(context.Company.WorkingHours, start, end, context.Configuration.BufferMinutes))
        {
            await Send.OkAsync(
                new GoogleCalendarAvailabilityResponse
                {
                    Available = false,
                    AlternativeSlots = [.. alternatives.Select(value => TimeOnly.FromDateTime(value.DateTime)).Take(1)],
                    UnavailabilityReason = "outside_working_hours",
                },
                cancellationToken);
            return;
        }

        var result = await calendarIntegration.CheckAvailabilityAsync(
            new CalendarAvailabilityRequest(
                context.CredentialReference,
                context.Configuration.CalendarId,
                start,
                end,
                request.PartySize,
                alternatives,
                context.Configuration.BufferMinutes),
            cancellationToken);

        await Send.OkAsync(
            new GoogleCalendarAvailabilityResponse
            {
                Available = result.Available,
                AlternativeSlots = [.. result.AlternativeStarts.Select(value => TimeOnly.FromDateTime(value.DateTime))],
                UnavailabilityReason = result.UnavailabilityReason,
            },
            cancellationToken);
    }
}

public sealed class CheckGoogleCalendarAvailabilityValidator
    : Validator<CheckGoogleCalendarAvailabilityRequest>
{
    public CheckGoogleCalendarAvailabilityValidator()
    {
        RuleFor(request => request.Date).NotEmpty();
        RuleFor(request => request.PartySize).GreaterThan(0);
    }
}
