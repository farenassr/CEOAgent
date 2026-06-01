using CeoAgent.Application.Errors;
using CeoAgent.Infrastructure.Entities.JsonDocuments;

namespace CeoAgent.Infrastructure.Scheduling;

public static class GoogleCalendarConfigValidator
{
    public static void Validate(GoogleCalendarConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (string.IsNullOrWhiteSpace(config.CalendarId))
        {
            throw new BusinessRuleException("invalid_calendar_id", "CalendarId is required.");
        }

        if (string.IsNullOrWhiteSpace(config.TimeZoneId))
        {
            throw new BusinessRuleException("invalid_time_zone_id", "TimeZoneId is required.");
        }

        ValidateTimeZone(config.TimeZoneId);

        if (config.SlotMinutes is < 5 or > 240)
        {
            throw new BusinessRuleException("invalid_slot_minutes", "SlotMinutes must be between 5 and 240.");
        }

        if (config.ReservationMinutes is < 5 or > 1440)
        {
            throw new BusinessRuleException("invalid_reservation_minutes", "ReservationMinutes must be between 5 and 1440.");
        }

        if (config.BufferMinutes is < 0 or > 240)
        {
            throw new BusinessRuleException("invalid_buffer_minutes", "BufferMinutes must be between 0 and 240.");
        }

        if (config.AdvanceBookingDays is < 0 or > 365)
        {
            throw new BusinessRuleException("invalid_advance_booking_days", "AdvanceBookingDays must be between 0 and 365.");
        }
    }

    private static void ValidateTimeZone(string timeZoneId)
    {
        try
        {
            _ = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        {
            throw new BusinessRuleException("invalid_time_zone_id", "TimeZoneId must identify a valid time zone.");
        }
    }
}
