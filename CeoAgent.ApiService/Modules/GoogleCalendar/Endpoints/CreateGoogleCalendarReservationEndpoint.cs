using CeoAgent.Application.Errors;
using CeoAgent.Infrastructure.Scheduling;
using CeoAgent.Integrations.Calendar;
using CeoAgent.Shared.Constants;
using CeoAgent.Shared.Request.GoogleCalendar;
using CeoAgent.Shared.Response.GoogleCalendar;
using FastEndpoints;
using FluentValidation;

namespace CeoAgent.ApiService.Modules.GoogleCalendar;

/// <summary>
/// Creates a Google Calendar reservation for a company-scoped calendar tool.
/// </summary>
public sealed class CreateGoogleCalendarReservationEndpoint(
    GoogleCalendarCompanyToolResolver resolver,
    ICalendarIntegration calendarIntegration,
    TimeProvider timeProvider)
    : Endpoint<CreateGoogleCalendarReservationRequest, GoogleCalendarReservationResponse>
{
    public override void Configure()
    {
        Post("/v1/admin/companies/{companyId}/google-calendar/reservations");
    }

    public override async Task HandleAsync(
        CreateGoogleCalendarReservationRequest request,
        CancellationToken cancellationToken)
    {
        var companyId = Route<Guid>("companyId");
        var context = await resolver.ResolveAsync(
            companyId,
            MvpToolKeys.CreateGoogleCalendarReservation,
            cancellationToken);

        if (!GoogleCalendarSchedulingPolicy.IsWithinAdvanceWindow(
            DateOnly.FromDateTime(request.Start.DateTime),
            context.Company.TimeZoneId,
            timeProvider.GetUtcNow(),
            context.Configuration.AdvanceBookingDays))
        {
            throw new BusinessRuleException(
                "outside_advance_booking_window",
                "Reservation must be within the configured advance booking window.");
        }

        if (!GoogleCalendarSchedulingPolicy.IsWithinWorkingHours(
            context.Company.WorkingHours,
            request.Start,
            request.End,
            context.Configuration.BufferMinutes))
        {
            throw new BusinessRuleException(
                "outside_working_hours",
                "Reservation must be within configured company working hours.");
        }

        var result = await calendarIntegration.CreateReservationAsync(
            new CalendarReservationRequest(
                CredentialReference: context.CredentialReference,
                CalendarId: context.Configuration.CalendarId,
                Start: request.Start,
                End: request.End,
                Summary: request.Summary,
                IdempotencyKey: request.IdempotencyKey,
                Description: BuildDescription(request),
                CustomerEmail: request.CustomerEmail),
            cancellationToken);

        await Send.OkAsync(
            new GoogleCalendarReservationResponse
            {
                EventId = result.EventId,
                EventUrl = result.EventUrl,
            },
            cancellationToken);
    }

    private static string? BuildDescription(CreateGoogleCalendarReservationRequest request)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(request.Description))
        {
            parts.Add(request.Description);
        }

        if (!string.IsNullOrWhiteSpace(request.CustomerName))
        {
            parts.Add($"Customer: {request.CustomerName}");
        }

        if (!string.IsNullOrWhiteSpace(request.CustomerEmail))
        {
            parts.Add($"Email: {request.CustomerEmail}");
        }

        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            parts.Add($"Notes: {request.Notes}");
        }

        return parts.Count == 0 ? null : string.Join(Environment.NewLine, parts);
    }
}

public sealed class CreateGoogleCalendarReservationValidator
    : Validator<CreateGoogleCalendarReservationRequest>
{
    public CreateGoogleCalendarReservationValidator()
    {
        RuleFor(request => request.Start).NotEmpty();
        RuleFor(request => request.End).GreaterThan(request => request.Start);
        RuleFor(request => request.Summary).NotEmpty().MaximumLength(300);
        RuleFor(request => request.Description).MaximumLength(2000);
        RuleFor(request => request.CustomerName).MaximumLength(300);
        RuleFor(request => request.CustomerEmail).EmailAddress().MaximumLength(300)
            .When(request => !string.IsNullOrWhiteSpace(request.CustomerEmail));
        RuleFor(request => request.Notes).MaximumLength(2000);
        RuleFor(request => request.IdempotencyKey).NotEmpty().MaximumLength(200);
    }
}
