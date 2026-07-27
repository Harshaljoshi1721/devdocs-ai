namespace DevDocsAI.Application.Common.Exceptions;

/// <summary>Input failed validation. Maps to HTTP 400.</summary>
public sealed class ValidationException(IReadOnlyDictionary<string, string[]> errors)
    : Exception("One or more validation errors occurred.")
{
    public IReadOnlyDictionary<string, string[]> Errors { get; } = errors;
}

/// <summary>Requested resource does not exist (or is not visible to the caller). Maps to HTTP 404.</summary>
public sealed class NotFoundException(string message) : Exception(message);

/// <summary>Request conflicts with existing state (e.g. duplicate email). Maps to HTTP 409.</summary>
public sealed class ConflictException(string message) : Exception(message);

/// <summary>Authentication failed (bad credentials / invalid token). Maps to HTTP 401.</summary>
public sealed class AuthenticationException(string message) : Exception(message);

/// <summary>Caller is authenticated but not allowed to perform the action. Maps to HTTP 403.</summary>
public sealed class ForbiddenAccessException(string message = "Access denied.") : Exception(message);
