namespace EcoStream.Application.Common;

public sealed class NotFoundException(string resource, string key)
    : Exception($"{resource} '{key}' was not found.");
