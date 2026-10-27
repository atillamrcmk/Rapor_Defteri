namespace RaporDefteri.Domain;

public sealed record DepartureDraft(string ReportText);

public sealed record ReturnDraft(
    string ReportText,
    Guid ReturnShiftId,
    IReadOnlyList<Guid> ClosingAbsenceIds,
    IReadOnlyList<Guid> StillOpenAbsenceIds);

public sealed record PersonDepartureState(
    Guid InmateId,
    Guid CustodyPeriodId,
    string FileNumber,
    string FullName,
    bool HasActiveCustody,
    bool HasOpenAbsence);

public static class DeparturePlanner
{
    public static RuleResult<DepartureDraft> Plan(
        DateTimeOffset eventAt,
        DateTimeOffset recordedAt,
        DateTimeOffset shiftStart,
        string? lateReason,
        TimeZoneInfo zone,
        TemporaryMovementKind kind,
        string destination,
        string reason,
        string? escort,
        DateTimeOffset? expectedReturn,
        IReadOnlyList<PersonDepartureState> people)
    {
        var errors = new List<string>();
        if (people.Count == 0)
            errors.Add("Hükümlü/tutuklu seçilmedi.");
        if (string.IsNullOrWhiteSpace(destination))
            errors.Add("Gidilen yer zorunludur.");
        if (string.IsNullOrWhiteSpace(reason))
            errors.Add("Gerekçe zorunludur.");
        if (expectedReturn is not null && expectedReturn < eventAt)
            errors.Add("Beklenen dönüş, çıkış zamanından önce olamaz.");

        errors.AddRange(TimeRules.ValidateEventTime(eventAt, recordedAt, shiftStart, lateReason));
        foreach (var person in people)
            errors.AddRange(MovementRules.ValidateDeparturePerson(person.HasActiveCustody, person.HasOpenAbsence, person.FileNumber, person.FullName));

        if (errors.Count > 0)
            return RuleResult<DepartureDraft>.Fail(errors);

        var local = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(eventAt, zone).DateTime);
        var text = ReportTextComposer.Departure(
            local,
            kind,
            people.Select(person => new PersonLine(person.FileNumber, person.FullName)).ToList(),
            destination,
            reason,
            escort);
        return RuleResult<DepartureDraft>.Ok(new DepartureDraft(text));
    }
}

public static class ReturnPlanner
{
    public static RuleResult<ReturnDraft> Plan(
        DateTimeOffset eventAt,
        DateTimeOffset recordedAt,
        DateTimeOffset shiftStart,
        Guid currentShiftId,
        string? lateReason,
        TimeZoneInfo zone,
        IReadOnlyList<OpenAbsenceState> selected,
        IReadOnlyList<Guid> openAbsenceIdsOfSameDeparture)
    {
        var errors = new List<string>();
        errors.AddRange(MovementRules.ValidateReturnGroup(selected));
        errors.AddRange(TimeRules.ValidateEventTime(eventAt, recordedAt, shiftStart, lateReason));
        foreach (var absence in selected)
            errors.AddRange(MovementRules.ValidateReturn(absence.Status, absence.DepartedAt, eventAt));

        if (errors.Count > 0)
            return RuleResult<ReturnDraft>.Fail(errors);

        var local = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(eventAt, zone).DateTime);
        var text = ReportTextComposer.Return(
            local,
            selected[0].Kind,
            selected.Select(item => new PersonLine(item.FileNumber, item.FullName)).ToList(),
            selected[0].Kind == TemporaryMovementKind.Other ? selected[0].Reason : null);
        var closing = selected.Select(item => item.AbsenceId).ToList();
        var stillOpen = MovementRules.RemainingOpen(openAbsenceIdsOfSameDeparture, closing);
        return RuleResult<ReturnDraft>.Ok(new ReturnDraft(text, currentShiftId, closing, stillOpen));
    }
}
