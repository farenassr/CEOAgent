using CeoAgent.Infrastructure.Implementation.AITools.GoogleCalendar;
using CeoAgent.ApiService.Infrastructure.OpenApi;
using CeoAgent.Application.Abstractions.AITools.GoogleCalendar;
using CeoAgent.Shared.Calendar;
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
    IGoogleCalendarIntegration calendarIntegration,
    TimeProvider timeProvider)
    : Endpoint<CheckGoogleCalendarAvailabilityRequest, GoogleCalendarAvailabilityResponse>
{
    public override void Configure()
    {
        Post("/v1/admin/companies/{organizationId}/google-calendar/availability");
        Description(builder => builder
            .WithTags(OpenApiConstants.Tags.GoogleCalendar)
            .WithSummary("Check Google Calendar Availability")
            .WithDescription("Checks whether a requested reservation slot is available for a company's configured Google Calendar tool. Use it before offering or confirming customer booking times."));
        Summary(summary =>
        {
            summary.Summary = "Check Google Calendar Availability";
            summary.Description = "Checks whether a requested reservation slot is available for a company's configured Google Calendar tool. Use it before offering or confirming customer booking times.";
        });
    }

    public override async Task HandleAsync(
        CheckGoogleCalendarAvailabilityRequest request,
        CancellationToken cancellationToken)
    {
        var organizationId = Route<Guid>("organizationId");
        var context = await resolver.ResolveAsync(
            organizationId,
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
        var searchWindowStart = start.AddHours(-GoogleCalendarSchedulingPolicy.AlternativeSearchWindowHours);
        var searchWindowEnd = start.AddHours(GoogleCalendarSchedulingPolicy.AlternativeSearchWindowHours);
        var alternatives = GoogleCalendarSchedulingPolicy.BuildAlternativeStarts(
            context.Company.WorkingHours,
            request.Date,
            start,
            context.Configuration.SlotMinutes,
            context.Configuration.ReservationMinutes,
            context.Configuration.BufferMinutes);
        var requestedSlotEligible = GoogleCalendarSchedulingPolicy.IsWithinWorkingHours(
            context.Company.WorkingHours,
            start,
            end,
            context.Configuration.BufferMinutes);

        if (!requestedSlotEligible && alternatives.Length == 0)
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

        var result = await calendarIntegration.CheckAvailabilityAsync(
            new CalendarAvailabilityRequest(
                context.CredentialReference,
                context.Configuration.CalendarId,
                start,
                end,
                searchWindowStart,
                searchWindowEnd,
                request.PartySize,
                alternatives,
                requestedSlotEligible,
                context.Configuration.BufferMinutes),
            cancellationToken);

        await Send.OkAsync(
            new GoogleCalendarAvailabilityResponse
            {
                Available = requestedSlotEligible && result.Available,
                AlternativeSlots = [.. result.AlternativeStarts.Select(value => TimeOnly.FromDateTime(value.DateTime))],
                UnavailabilityReason = requestedSlotEligible ? result.UnavailabilityReason : "outside_working_hours",
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
