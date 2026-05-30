namespace CeoAgent.Integrations.Speech;

public sealed record SpeechSynthesisRequest(
    string Text,
    string Language,
    string VoiceName,
    string ModelName);
