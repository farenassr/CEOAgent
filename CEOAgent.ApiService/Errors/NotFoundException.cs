namespace CEOAgent.ApiService;

public sealed class NotFoundException(string resource, object key)
    : Exception($"{resource} {key} not found");
