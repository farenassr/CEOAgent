using CeoAgent.Application.Errors;
using CeoAgent.Shared.Request.GoogleCalendar;
using CeoAgent.Shared.Response.GoogleCalendar;
using FastEndpoints;
using FluentValidation;

namespace CeoAgent.ApiService.Modules.GoogleCalendar;

/// <summary>
/// Creates a Google Calendar reservation for a company-scoped calendar tool.
/// </summary>
public sealed class CreateGoogleCalendarReservationEndpoint
    : Endpoint<CreateGoogleCalendarReservationRequest, GoogleCalendarReservationResponse>
{
    public override void Configure()
    {
        Post("/v1/admin/companies/{companyId}/google-calendar/reservations");
    }

    public override Task HandleAsync(
        CreateGoogleCalendarReservationRequest request,
        CancellationToken cancellationToken)
    {
        throw new BusinessRuleException(
            "admin_google_calendar_mutation_disabled",
            "Create reservations through the audited Google Calendar tool execution path.");
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
        RuleFor(request => request.CustomerName).NotEmpty().MaximumLength(300);
        RuleFor(request => request.CustomerEmail).EmailAddress().MaximumLength(300)
            .When(request => !string.IsNullOrWhiteSpace(request.CustomerEmail));
        RuleFor(request => request.Notes).MaximumLength(2000);
        RuleFor(request => request.IdempotencyKey).NotEmpty().MaximumLength(200);
    }
}
