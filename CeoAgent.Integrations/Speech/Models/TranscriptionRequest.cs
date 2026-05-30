namespace CeoAgent.Integrations.Speech;

public sealed record TranscriptionRequest(
    Stream Audio,
    string ContentType,
    string? Language,
    string ModelName);
