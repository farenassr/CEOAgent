using System.Text.Json.Serialization;

namespace CeoAgent.Adapters.GoogleCalendar.Abstractions;

public sealed record GoogleFreeBusyRequest(
    [property: JsonPropertyName("timeMin")] DateTimeOffset TimeMin,
    [property: JsonPropertyName("timeMax")] DateTimeOffset TimeMax,
    [property: JsonPropertyName("items")] IReadOnlyList<GoogleFreeBusyItem> Items);

public sealed record GoogleFreeBusyItem(
    [property: JsonPropertyName("id")] string Id);

public sealed record GoogleFreeBusyResponse(
    [property: JsonPropertyName("calendars")] Dictionary<string, GoogleCalendarBusyInfo>? Calendars);

public sealed record GoogleCalendarBusyInfo(
    [property: JsonPropertyName("busy")] IReadOnlyList<GoogleBusyRange>? Busy);

public sealed record GoogleBusyRange(
    [property: JsonPropertyName("start")] DateTimeOffset Start,
    [property: JsonPropertyName("end")] DateTimeOffset End);

public sealed record GoogleCalendarEventRequest(
    [property: JsonPropertyName("summary")] string Summary,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("start")] GoogleCalendarEventDateTime Start,
    [property: JsonPropertyName("end")] GoogleCalendarEventDateTime End,
    [property: JsonPropertyName("extendedProperties")] GoogleExtendedProperties ExtendedProperties);

public sealed record GoogleCalendarEventDateTime(
    [property: JsonPropertyName("dateTime")] DateTimeOffset DateTime);

public sealed record GoogleExtendedProperties(
    [property: JsonPropertyName("private")] Dictionary<string, string> Private);

public sealed record GoogleCalendarEventResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("htmlLink")] string HtmlLink);
