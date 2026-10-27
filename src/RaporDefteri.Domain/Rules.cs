namespace RaporDefteri.Domain;

public static class TimeRules
{
    public static bool IsTooFarInFuture(DateTimeOffset eventAt, DateTimeOffset recordedAt) =>
        eventAt > recordedAt.AddMinutes(2);

    public static bool RequiresLateReason(DateTimeOffset eventAt, DateTimeOffset recordedAt, DateTimeOffset shiftStart) =>
        eventAt < recordedAt.AddMinutes(-10) || eventAt < shiftStart;

    public static IReadOnlyList<string> ValidateEventTime(DateTimeOffset eventAt, DateTimeOffset recordedAt, DateTimeOffset shiftStart, string? lateReason)
    {
        var errors = new List<string>();
        if (IsTooFarInFuture(eventAt, recordedAt))
            errors.Add("Olay saati sunucu saatinden ileri olamaz.");
        if (RequiresLateReason(eventAt, recordedAt, shiftStart) && string.IsNullOrWhiteSpace(lateReason))
            errors.Add("Geç kayıt gerekçesi zorunludur. Olay saati ile sisteme giriş saati ayrı tutulur.");
        return errors;
    }
}

public static class ShiftRules
{
    public static IReadOnlyList<string> ValidateNew(DateTimeOffset start, DateTimeOffset end, bool anotherActive)
    {
        var errors = new List<string>();
        if (end <= start)
            errors.Add("Bitiş, başlangıçtan sonra olmalıdır. Gece yarısını aşan vardiyada bitiş ertesi günün saatidir.");
        if (anotherActive)
            errors.Add("Açık veya devre hazır bir vardiya varken yenisi açılamaz.");
        return errors;
    }

    public static IReadOnlyList<string> ValidateAccept(ShiftStatus status)
    {
        if (status != ShiftStatus.ReadyForHandover)
            return ["Devralma yalnızca devre hazır vardiyada yapılabilir."];
        return [];
    }

    public static bool AcceptsRecords(ShiftStatus status) =>
        status is ShiftStatus.Open or ShiftStatus.ReadyForHandover;
}

public static class MovementRules
{
    public static IReadOnlyList<string> ValidateDeparturePerson(bool hasActiveCustody, bool hasOpenAbsence, string fileNumber, string fullName)
    {
        var errors = new List<string>();
        if (!hasActiveCustody)
            errors.Add($"{fileNumber} numaralı {fullName} için aktif kalış yok.");
        if (hasOpenAbsence)
            errors.Add($"{fileNumber} numaralı {fullName} için açık kurum dışı hareket var. İkinci açık çıkış kaydedilemez.");
        return errors;
    }

    public static IReadOnlyList<string> ValidateReturn(TemporaryAbsenceStatus status, DateTimeOffset departedAt, DateTimeOffset returnedAt)
    {
        var errors = new List<string>();
        if (status != TemporaryAbsenceStatus.Open)
            errors.Add("Bu hareket zaten kapanmış. Aynı dönüş ikinci kez kaydedilemez.");
        if (returnedAt < departedAt)
            errors.Add("Dönüş zamanı çıkış zamanından önce olamaz.");
        return errors;
    }

    public static IReadOnlyList<string> ValidateReturnGroup(IReadOnlyList<OpenAbsenceState> selected)
    {
        if (selected.Count == 0)
            return ["Dönen hükümlü/tutuklu seçilmedi."];

        var kind = selected[0].Kind;
        var destination = TurkishText.NormalizeName(selected[0].Destination);
        if (selected.Any(item => item.Kind != kind || TurkishText.NormalizeName(item.Destination) != destination))
            return ["Aynı dönüş kaydında kişiler aynı hareket türünde ve aynı gidilen yerde olmalıdır."];
        return [];
    }

    public static IReadOnlyList<Guid> RemainingOpen(IReadOnlyList<Guid> openAbsenceIds, IReadOnlyCollection<Guid> returningAbsenceIds) =>
        openAbsenceIds.Where(id => !returningAbsenceIds.Contains(id)).ToList();

    public static IReadOnlyList<string> ValidatePermanentExit(bool hasActiveCustody, bool hasOpenAbsence)
    {
        var errors = new List<string>();
        if (!hasActiveCustody)
            errors.Add("Aktif kalış yok.");
        if (hasOpenAbsence)
            errors.Add("Açık kurum dışı hareket varken tahliye veya sevk kaydedilemez. Önce dönüş kaydı gerekir.");
        return errors;
    }
}

public static class CorrectionRules
{
    public static IReadOnlyList<string> Validate(ShiftStatus shift, bool isSupervisor, bool isOperator, string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return ["Düzeltme gerekçesi zorunludur."];
        if (shift == ShiftStatus.Closed && !isSupervisor)
            return ["Kapanmış vardiya kaydı gerekçesiz veya yetkisiz değiştirilemez."];
        if (shift != ShiftStatus.Closed && !(isOperator || isSupervisor))
            return ["Bu kayıt için düzeltme yetkiniz yok."];
        return [];
    }
}

public static class ConcurrencyRules
{
    public static bool IsStale(uint submittedToken, uint currentToken) => submittedToken != currentToken;
}

public static class ReplayGuard
{
    public static bool IsReplay(Guid? existingEntryId) => existingEntryId is not null;
}

public static class ReportPeriod
{
    public static bool InShift(DateTimeOffset eventAt, DateTimeOffset start, DateTimeOffset end) =>
        eventAt >= start && eventAt < end;

    public static (DateTimeOffset Start, DateTimeOffset End) DayBounds(DateOnly day, TimeZoneInfo zone)
    {
        var local = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var start = new DateTimeOffset(local, zone.GetUtcOffset(local));
        return (start, start.AddDays(1));
    }

    public static bool InDailyReport(DateTimeOffset eventAt, DateOnly day, TimeZoneInfo zone)
    {
        var bounds = DayBounds(day, zone);
        return eventAt >= bounds.Start && eventAt < bounds.End;
    }
}

public static class ReturnWatchRules
{
    public static ReturnWatch Evaluate(DateTimeOffset? expectedReturn, DateTimeOffset now)
    {
        if (expectedReturn is null)
            return ReturnWatch.NotScheduled;
        return now > expectedReturn.Value ? ReturnWatch.ExpectedTimePassed : ReturnWatch.Waiting;
    }

    public static string Label(ReturnWatch watch) => watch switch
    {
        ReturnWatch.NotScheduled => "Dönüş saati belirlenmedi",
        ReturnWatch.Waiting => "Dönüş bekleniyor",
        ReturnWatch.ExpectedTimePassed => "Beklenen saat geçti",
        _ => "Dönüş bekleniyor"
    };
}

public static class PersonSearch
{
    public static IReadOnlyList<PersonHit> Present(IEnumerable<PersonHit> hits) =>
        hits.DistinctBy(hit => hit.Id).ToList();
}

public readonly record struct PersonHit(Guid Id, string FileNumber, string FullName, string? Ward);

public sealed record OpenAbsenceState(
    Guid AbsenceId,
    Guid InmateId,
    string FileNumber,
    string FullName,
    TemporaryMovementKind Kind,
    string Destination,
    DateTimeOffset DepartedAt,
    Guid DepartureShiftId,
    Guid DepartureReportEntryId,
    string Reason,
    TemporaryAbsenceStatus Status);

public enum VehiclePlanKind
{
    OpenVisit = 1,
    CloseVisit = 2,
    Standalone = 3,
    Reject = 4
}

public readonly record struct VehiclePlan(VehiclePlanKind Kind, LocationInference Inference, string? Error)
{
    public static VehiclePlan Reject(string error) => new(VehiclePlanKind.Reject, LocationInference.None, error);
}

public static class VehicleRules
{
    public static VehiclePlan Plan(VehicleBaseLocation location, PassageDirection direction, bool hasOpenVisit, LocationInference openInference)
    {
        if (location == VehicleBaseLocation.Unspecified)
            return new VehiclePlan(VehiclePlanKind.Standalone, LocationInference.None, null);

        if (location == VehicleBaseLocation.External && direction == PassageDirection.Entry)
        {
            if (hasOpenVisit)
                return VehiclePlan.Reject("Bu plaka için kapanmamış bir sefer var.");
            return new VehiclePlan(VehiclePlanKind.OpenVisit, LocationInference.AwaitingExit, null);
        }

        if (location == VehicleBaseLocation.External && direction == PassageDirection.Exit)
        {
            if (hasOpenVisit && openInference == LocationInference.AwaitingExit)
                return new VehiclePlan(VehiclePlanKind.CloseVisit, LocationInference.None, null);
            if (hasOpenVisit)
                return VehiclePlan.Reject("Açık sefer bu çıkışla kapatılamaz.");
            return new VehiclePlan(VehiclePlanKind.Standalone, LocationInference.None, null);
        }

        if (direction == PassageDirection.Exit)
        {
            if (hasOpenVisit)
                return VehiclePlan.Reject("Bu plaka için kapanmamış bir sefer var.");
            return new VehiclePlan(VehiclePlanKind.OpenVisit, LocationInference.AwaitingReturn, null);
        }

        if (hasOpenVisit && openInference == LocationInference.AwaitingReturn)
            return new VehiclePlan(VehiclePlanKind.CloseVisit, LocationInference.None, null);
        if (hasOpenVisit)
            return VehiclePlan.Reject("Açık sefer bu girişle kapatılamaz.");
        return new VehiclePlan(VehiclePlanKind.Standalone, LocationInference.None, null);
    }
}
