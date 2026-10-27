namespace RaporDefteri.Application;

public static class AppRoles
{
    public const string Operator = "Operatör";
    public const string ShiftSupervisor = "Vardiya Sorumlusu";
    public const string Administrator = "Yönetici";
    public const string ReadOnly = "Salt Okunur";

    public static readonly string[] All = [Operator, ShiftSupervisor, Administrator, ReadOnly];
}

public static class Policies
{
    public const string ViewRecords = "KayitGoruntule";
    public const string CreateRecords = "KayitOlustur";
    public const string ManageShift = "VardiyaYonet";
    public const string CorrectRecords = "KayitDuzelt";
    public const string ManageUsers = "KullaniciYonet";
    public const string ManageDefinitions = "TanimYonet";
}

public sealed record Actor(Guid UserId, string UserName, bool IsOperator, bool IsSupervisor, bool IsAdministrator);

public sealed class OperationResult<T>
{
    private OperationResult(bool succeeded, T? value, IReadOnlyList<string> errors, bool concurrencyConflict)
    {
        Succeeded = succeeded;
        Value = value;
        Errors = errors;
        ConcurrencyConflict = concurrencyConflict;
    }

    public bool Succeeded { get; }
    public T? Value { get; }
    public IReadOnlyList<string> Errors { get; }
    public bool ConcurrencyConflict { get; }

    public static OperationResult<T> Ok(T value) => new(true, value, [], false);
    public static OperationResult<T> Fail(params string[] errors) => new(false, default, errors, false);
    public static OperationResult<T> Fail(IReadOnlyList<string> errors) => new(false, default, errors, false);
    public static OperationResult<T> Conflict() => new(false, default, ["Kayıt başka bir işlemle değişti. Yeniden açıp tekrar deneyin. Değişiklik yazılmadı."], true);
}

public sealed class UniqueConstraintException : Exception
{
    public UniqueConstraintException(string? constraint) : base("Tekil kısıt ihlali.") => Constraint = constraint;
    public string? Constraint { get; }
    public bool IsIdempotency => Constraint?.Contains("idempotency", StringComparison.OrdinalIgnoreCase) == true;
    public bool IsOpenAbsence => Constraint?.Contains("open_absence", StringComparison.OrdinalIgnoreCase) == true;
}

public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException() : base("Eşzamanlı değişiklik.")
    {
    }
}
