using System.Net.Sockets;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using RaporDefteri.Application;
using RaporDefteri.Domain;

namespace RaporDefteri.Infrastructure;

public sealed class InstitutionClock(TimeZoneInfo zone) : IInstitutionClock
{
    public TimeZoneInfo Zone { get; } = zone;
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public DateTimeOffset FromLocal(DateTime unspecifiedLocal)
    {
        var local = DateTime.SpecifyKind(unspecifiedLocal, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, Zone.GetUtcOffset(local)).ToUniversalTime();
    }

    public DateTime ToLocal(DateTimeOffset value) => TimeZoneInfo.ConvertTime(value, Zone).DateTime;

    public string Format(DateTimeOffset value) => ToLocal(value).ToString("dd.MM.yyyy HH:mm");

    public static TimeZoneInfo Resolve(string? configured)
    {
        foreach (var id in new[] { configured, "Europe/Istanbul", "Turkey Standard Time" })
        {
            if (string.IsNullOrWhiteSpace(id))
                continue;
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(id);
            }
            catch (TimeZoneNotFoundException)
            {
            }
            catch (InvalidTimeZoneException)
            {
            }
        }

        return TimeZoneInfo.CreateCustomTimeZone("TR-offset", TimeSpan.FromHours(3), "TR", "TR");
    }
}

public sealed class InstitutionProfile(string name, string unitName) : IInstitutionProfile
{
    public string Name { get; } = name;
    public string UnitName { get; } = unitName;
}

public static class DatabaseExceptionClassifier
{
    public static bool IsConnectivity(Exception? exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException postgres)
                return IsConnectivityState(postgres.SqlState);
            if (current is NpgsqlException or TimeoutException or SocketException or IOException)
                return true;
        }

        return false;
    }

    public static bool IsConnectivityState(string? sqlState) =>
        sqlState is not null && (sqlState.StartsWith("08", StringComparison.Ordinal) || sqlState is "57P01" or "57P02" or "57P03" or "53300");
}

public sealed class UserAdmin(UserManager<AppUser> users, IRaporStore store, IInstitutionClock clock) : IUserAdmin
{
    public Task<bool> AnyUserAsync(CancellationToken ct) => users.Users.AnyAsync(ct);

    public async Task<IReadOnlyList<UserAccountRow>> ListAsync(CancellationToken ct)
    {
        var accounts = await users.Users.OrderBy(user => user.DisplayName).ToListAsync(ct);
        var rows = new List<UserAccountRow>();
        foreach (var account in accounts)
        {
            var roles = await users.GetRolesAsync(account);
            rows.Add(new UserAccountRow(account.Id, account.UserName ?? string.Empty, account.DisplayName, account.IsActive, roles.ToList()));
        }

        return rows;
    }

    public async Task<OperationResult<Guid>> CreateAsync(string userName, string displayName, string password, IReadOnlyCollection<string> roles, CancellationToken ct)
    {
        var user = new AppUser
        {
            Id = Guid.CreateVersion7(),
            UserName = userName.Trim(),
            DisplayName = displayName.Trim(),
            IsActive = true,
            CreatedAt = clock.UtcNow
        };
        var created = await users.CreateAsync(user, password);
        if (!created.Succeeded)
            return OperationResult<Guid>.Fail(created.Errors.Select(Turkish).ToArray());
        if (roles.Count > 0)
            await users.AddToRolesAsync(user, roles);
        await AuditAsync(user.Id, "Kullanıcı oluşturuldu", user.Id, string.Join(", ", roles), ct);
        return OperationResult<Guid>.Ok(user.Id);
    }

    public async Task<OperationResult<bool>> UpdateAsync(Guid userId, string displayName, IReadOnlyCollection<string> roles, bool isActive, string? newPassword, CancellationToken ct)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null)
            return OperationResult<bool>.Fail("Kullanıcı bulunamadı.");
        user.DisplayName = displayName.Trim();
        user.IsActive = isActive;
        var updated = await users.UpdateAsync(user);
        if (!updated.Succeeded)
            return OperationResult<bool>.Fail(updated.Errors.Select(Turkish).ToArray());
        var current = await users.GetRolesAsync(user);
        await users.RemoveFromRolesAsync(user, current);
        if (roles.Count > 0)
            await users.AddToRolesAsync(user, roles);
        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            var token = await users.GeneratePasswordResetTokenAsync(user);
            var reset = await users.ResetPasswordAsync(user, token, newPassword);
            if (!reset.Succeeded)
                return OperationResult<bool>.Fail(reset.Errors.Select(Turkish).ToArray());
            await AuditAsync(user.Id, "Parola sıfırlandı", user.Id, "Parola değeri kayda yazılmadı.", ct);
        }

        if (!isActive)
            await users.UpdateSecurityStampAsync(user);
        await AuditAsync(user.Id, "Kullanıcı güncellendi", user.Id, string.Join(", ", roles), ct);
        return OperationResult<bool>.Ok(true);
    }

    private async Task AuditAsync(Guid userId, string action, Guid entityId, string details, CancellationToken ct)
    {
        await store.AddAuditAsync(new AuditEvent
        {
            Id = Guid.CreateVersion7(),
            OccurredAt = clock.UtcNow,
            UserId = userId,
            Action = action,
            EntityName = "User",
            EntityId = entityId.ToString(),
            Details = details
        }, ct);
        await store.SaveAsync(ct);
    }

    private static string Turkish(IdentityError error) => error.Code switch
    {
        "DuplicateUserName" => "Bu kullanıcı adı kayıtlı.",
        "PasswordTooShort" => "Parola en az 10 karakter olmalıdır.",
        "PasswordRequiresDigit" => "Parolada rakam olmalıdır.",
        "PasswordRequiresUpper" => "Parolada büyük harf olmalıdır.",
        "PasswordRequiresLower" => "Parolada küçük harf olmalıdır.",
        "PasswordRequiresNonAlphanumeric" => "Parolada simge olmalıdır.",
        _ => "İşlem tamamlanamadı."
    };
}

public sealed class DemoData(IRaporStore store, InmateService inmates, UserManager<AppUser> users) : IDemoData
{
    public async Task<string> SeedAsync(CancellationToken ct)
    {
        if (await store.FileTakenAsync(TurkishText.NormalizeFileNumber("KURMACA-001"), null, ct))
            return "Kurmaca kişi kartları zaten var. Yeni kayıt üretilmedi.";
        var shift = await store.ActiveShiftAsync(ct);
        if (shift is null)
            return "Kurmaca veri yüklenmedi. Önce bir vardiya açın. Bu komut üretim başlangıcında çalışmaz.";
        var admin = await users.Users.OrderBy(user => user.CreatedAt).FirstOrDefaultAsync(ct);
        if (admin is null)
            return "Kurmaca veri yüklenmedi. Önce kurulum kullanıcısı oluşturun.";

        var actor = new Actor(admin.Id, admin.UserName ?? "kurulum", true, true, true);
        var now = DateTimeOffset.UtcNow;
        var samples = new (string File, string First, string Last, string Ward)[]
        {
            ("KURMACA-001", "Kurmaca Ali", "Deneme", "A-1"),
            ("KURMACA-002", "Kurmaca Ali", "Deneme", "A-2"),
            ("KURMACA-003", "Kurmaca Veli", "Deneme", "B-1")
        };
        foreach (var sample in samples)
        {
            var result = await inmates.AdmitAsync(new AdmissionCommand(now, null, "Kurmaca demo kaydı. Gerçek kişi değildir.", sample.File, sample.First, sample.Last, sample.Ward, null, Guid.NewGuid().ToString("N")), actor, ct);
            if (!result.Succeeded)
                return string.Join(" ", result.Errors);
        }

        return "Üç kurmaca hükümlü/tutuklu kartı ve kabul kaydı eklendi. Gerçek kişi verisi yoktur.";
    }
}

public static class RoleSeeder
{
    private static int _ready;

    public static async Task EnsureAsync(IServiceProvider services, CancellationToken ct)
    {
        if (Volatile.Read(ref _ready) == 1)
            return;
        await using var scope = services.CreateAsyncScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<AppRole>>();
        foreach (var name in AppRoles.All)
        {
            if (await roles.RoleExistsAsync(name))
                continue;
            var created = await roles.CreateAsync(new AppRole { Id = Guid.CreateVersion7(), Name = name });
            if (!created.Succeeded)
                throw new InvalidOperationException("Roller oluşturulamadı.");
        }

        Interlocked.Exchange(ref _ready, 1);
    }
}
