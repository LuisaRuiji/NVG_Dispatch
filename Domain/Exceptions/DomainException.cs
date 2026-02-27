using System.Net;

namespace NVGInventory.Domain.Exceptions;

public class DomainException : Exception
{
    public string ErrorCode { get; }
    public int StatusCode { get; }

    public DomainException(
        string message,
        string errorCode = "BUSINESS_RULE_VIOLATION",
        int statusCode = (int)HttpStatusCode.BadRequest) : base(message)
    {
        ErrorCode = errorCode;
        StatusCode = statusCode;
    }
}

public sealed class BusinessRuleViolationException : DomainException
{
    public BusinessRuleViolationException(string message)
        : base(message, "BUSINESS_RULE_VIOLATION", (int)HttpStatusCode.BadRequest)
    {
    }
}

public sealed class NotFoundException : DomainException
{
    public NotFoundException(string message)
        : base(message, "NOT_FOUND", (int)HttpStatusCode.NotFound)
    {
    }
}

public sealed class UnauthorizedDomainException : DomainException
{
    public UnauthorizedDomainException(string message)
        : base(message, "UNAUTHORIZED", (int)HttpStatusCode.Unauthorized)
    {
    }
}

public sealed class ForbiddenDomainException : DomainException
{
    public ForbiddenDomainException(string message)
        : base(message, "FORBIDDEN", (int)HttpStatusCode.Forbidden)
    {
    }
}

public sealed class ConcurrencyConflictException : DomainException
{
    public ConcurrencyConflictException(string message)
        : base(message, "CONCURRENCY_CONFLICT", (int)HttpStatusCode.Conflict)
    {
    }
}

public sealed class ConflictDomainException : DomainException
{
    public ConflictDomainException(string message)
        : base(message, "CONFLICT", (int)HttpStatusCode.Conflict)
    {
    }
}

public sealed class ModuleDisabledException : DomainException
{
    public ModuleDisabledException(string message)
        : base(message, "MODULE_DISABLED", (int)HttpStatusCode.ServiceUnavailable)
    {
    }
}
