namespace CeoAgent.Integrations.Speech;

public sealed record SpeechSynthesisResult(
    Stream Audio,
    string ContentType,
    string Extension);
