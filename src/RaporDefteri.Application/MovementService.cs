using RaporDefteri.Domain;

namespace RaporDefteri.Application;

public static class Persist
{
    public static async Task<OperationResult<Guid>> SaveNewAsync(IRaporStore store, Guid entryId, string idempotencyKey, CancellationToken ct)
    {
        try
        {
            await store.SaveAsync(ct);
            return OperationResult<Guid>.Ok(entryId);
        }
        catch (UniqueConstraintException ex) when (ex.IsIdempotency)
        {
            store.Clear();
            var existing = await store.ByIdempotencyAsync(idempotencyKey, ct);
            return existing is null
                ? OperationResult<Guid>.Fail("Yinelenen istek okunamadı. Kayıt başarılı sayılmadı.")
                : OperationResult<Guid>.Ok(existing.Id);
        }
        catch (UniqueConstraintException)
        {
            return OperationResult<Guid>.Fail("Kayıt çakıştı ve yazılmadı.");
        }
        catch (ConcurrencyConflictException)
        {
            return OperationResult<Guid>.Conflict();
        }
    }

    public static async Task<Shift?> RequireWritableShiftAsync(IRaporStore store, CancellationToken ct)
    {
        var shift = await store.ActiveShiftAsync(ct);
        return shift is not null && ShiftRules.AcceptsRecords(shift.Status) ? shift : null;
    }
}

public sealed class MovementService(IRaporStore store, IInstitutionClock clock)
{
    public async Task<OperationResult<string>> PreviewDepartureAsync(DepartCommand command, CancellationToken ct)
    {
        var shift = await Persist.RequireWritableShiftAsync(store, ct);
        if (shift is null)
            return OperationResult<string>.Fail("Açık vardiya yok. Kayıt yazılmadı.");
        var states = await LoadPeopleAsync(command.InmateIds, ct);
        if (states.Error is not null)
            return OperationResult<string>.Fail(states.Error);
        var plan = DeparturePlanner.Plan(command.EventAt, clock.UtcNow, shift.StartsAt, command.LateReason, clock.Zone, command.Kind, command.Destination, command.Reason, command.Escort, command.ExpectedReturn, states.People);
        return plan.Succeeded
            ? OperationResult<string>.Ok(plan.Value!.ReportText)
            : OperationResult<string>.Fail(plan.Errors);
    }

    public async Task<OperationResult<Guid>> DepartAsync(DepartCommand command, Actor actor, CancellationToken ct)
    {
        var replay = await store.ByIdempotencyAsync(command.IdempotencyKey, ct);
        if (replay is not null)
            return OperationResult<Guid>.Ok(replay.Id);

        var preview = await PreviewDepartureAsync(command, ct);
        if (!preview.Succeeded || preview.Value is null)
            return OperationResult<Guid>.Fail(preview.Errors);

        if (command.VehicleVisitId is Guid visitId && await store.VisitAsync(visitId, ct) is null)
            return OperationResult<Guid>.Fail("Bağlanacak araç seferi bulunamadı. Çıkış yazılmadı.");

        var shift = (await store.ActiveShiftAsync(ct))!;
        var inmates = new List<Inmate>();
        foreach (var id in command.InmateIds.Distinct())
            inmates.Add((await store.InmateAsync(id, ct))!);

        var now = clock.UtcNow;
        var entry = ReportEntry.Create(await store.NextNumberAsync(ct), command.EventAt, now, EntryKind.DepartureType(command.Kind), shift.Id, actor.UserId, preview.Value, command.AdditionalNote, command.LateReason, command.IdempotencyKey);
        foreach (var inmate in inmates)
        {
            entry.People.Add(new ReportEntryInmate { ReportEntryId = entry.Id, InmateId = inmate.Id });
            var custody = inmate.ActiveCustody()!;
            await store.AddAbsenceAsync(TemporaryAbsence.Open(inmate.Id, custody.Id, command.Kind, command.Destination, command.Reason, command.EventAt, command.ExpectedReturn, entry.Id, shift.Id, command.VehicleVisitId, command.Escort), ct);
        }

        await store.AddEntryAsync(entry, ct);
        return await Persist.SaveNewAsync(store, entry.Id, command.IdempotencyKey, ct);
    }

    public async Task<OperationResult<string>> PreviewReturnAsync(ReturnCommand command, CancellationToken ct)
    {
        var prepared = await PrepareReturnAsync(command, ct);
        return prepared.Succeeded
            ? OperationResult<string>.Ok(prepared.Value!.ReportText)
            : OperationResult<string>.Fail(prepared.Errors);
    }

    public async Task<OperationResult<Guid>> ReturnAsync(ReturnCommand command, Actor actor, CancellationToken ct)
    {
        var replay = await store.ByIdempotencyAsync(command.IdempotencyKey, ct);
        if (replay is not null)
            return OperationResult<Guid>.Ok(replay.Id);

        var prepared = await PrepareReturnAsync(command, ct);
        if (!prepared.Succeeded || prepared.Value is null)
            return OperationResult<Guid>.Fail(prepared.Errors);

        if (command.VehicleVisitId is Guid visitId && await store.VisitAsync(visitId, ct) is null)
            return OperationResult<Guid>.Fail("Bağlanacak araç seferi bulunamadı. Dönüş yazılmadı.");

        var shift = (await store.ActiveShiftAsync(ct))!;
        var absencesForKind = await store.AbsencesAsync(command.AbsenceIds, ct);
        var now = clock.UtcNow;
        var entry = ReportEntry.Create(await store.NextNumberAsync(ct), command.EventAt, now, EntryKind.ReturnType(absencesForKind[0].Kind), shift.Id, actor.UserId, prepared.Value.ReportText, command.AdditionalNote, command.LateReason, command.IdempotencyKey);
        var absences = absencesForKind;
        foreach (var absence in absences)
        {
            absence.MarkReturned(command.EventAt, entry.Id, shift.Id, command.VehicleVisitId);
            entry.People.Add(new ReportEntryInmate { ReportEntryId = entry.Id, InmateId = absence.InmateId });
        }

        await store.AddEntryAsync(entry, ct);
        return await Persist.SaveNewAsync(store, entry.Id, command.IdempotencyKey, ct);
    }

    private async Task<(List<PersonDepartureState> People, string[]? Error)> LoadPeopleAsync(IReadOnlyList<Guid> ids, CancellationToken ct)
    {
        if (ids.Count == 0)
            return ([], ["Hükümlü/tutuklu seçilmedi."]);
        var people = new List<PersonDepartureState>();
        foreach (var id in ids.Distinct())
        {
            var inmate = await store.InmateAsync(id, ct);
            if (inmate is null)
                return ([], ["Seçilen hükümlü/tutuklu bulunamadı."]);
            var custody = inmate.ActiveCustody();
            people.Add(new PersonDepartureState(inmate.Id, custody?.Id ?? Guid.Empty, inmate.FileNumber, inmate.FullName, custody is not null, await store.HasOpenAbsenceAsync(id, ct)));
        }

        return (people, null);
    }

    private async Task<OperationResult<ReturnDraft>> PrepareReturnAsync(ReturnCommand command, CancellationToken ct)
    {
        var shift = await Persist.RequireWritableShiftAsync(store, ct);
        if (shift is null)
            return OperationResult<ReturnDraft>.Fail("Açık vardiya yok. Kayıt yazılmadı.");

        var absences = await store.AbsencesAsync(command.AbsenceIds.Distinct().ToList(), ct);
        if (absences.Count != command.AbsenceIds.Distinct().Count())
            return OperationResult<ReturnDraft>.Fail("Seçilen hareket bulunamadı.");

        var states = new List<OpenAbsenceState>();
        foreach (var absence in absences)
        {
            var inmate = await store.InmateAsync(absence.InmateId, ct);
            if (inmate is null)
                return OperationResult<ReturnDraft>.Fail("Hareketin kişisi bulunamadı.");
            states.Add(new OpenAbsenceState(absence.Id, inmate.Id, inmate.FileNumber, inmate.FullName, absence.Kind, absence.Destination, absence.DepartedAt, absence.DepartureShiftId, absence.DepartureReportEntryId, absence.Reason, absence.Status));
        }

        var openIds = await store.OpenIdsForDepartureAsync(states[0].DepartureReportEntryId, ct);
        var plan = ReturnPlanner.Plan(command.EventAt, clock.UtcNow, shift.StartsAt, shift.Id, command.LateReason, clock.Zone, states, openIds);
        if (!plan.Succeeded || plan.Value is null)
            return OperationResult<ReturnDraft>.Fail(plan.Errors);
        return OperationResult<ReturnDraft>.Ok(plan.Value);
    }
}
