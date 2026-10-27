namespace RaporDefteri.Domain;

public sealed class DomainRuleException : Exception
{
    public DomainRuleException(string message) : base(message)
    {
    }
}

public sealed class RuleResult<T>
{
    private RuleResult(bool succeeded, T? value, IReadOnlyList<string> errors)
    {
        Succeeded = succeeded;
        Value = value;
        Errors = errors;
    }

    public bool Succeeded { get; }
    public T? Value { get; }
    public IReadOnlyList<string> Errors { get; }

    public static RuleResult<T> Ok(T value) => new(true, value, []);
    public static RuleResult<T> Fail(string error) => new(false, default, [error]);
    public static RuleResult<T> Fail(IReadOnlyList<string> errors) => new(false, default, errors);
}
