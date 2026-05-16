namespace CEOAgent.Application.Errors;

public sealed class BusinessRuleException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
