namespace CeoAgent.Integrations.Speech;

public sealed record TranscriptionResult(
    string Text,
    string? Language,
    TimeSpan? Duration);
